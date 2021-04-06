﻿namespace yeenland_on_s3_push

open Amazon
open Amazon.DynamoDBv2.DataModel
open Amazon.DynamoDBv2.DocumentModel
open Amazon.Lambda.Core
open Amazon.Lambda.S3Events
open Amazon.Lambda.Serialization.SystemTextJson
open Amazon.S3.Model
open Amazon.S3.Util
open FSharpPlus.Data
open yeenland
open yeenland.DynamoDB
open yeenland.S3
open yeenland.Services

module Function =

    let FunctionHandler (event: S3Event) (_: ILambdaContext) =
        let services =
            new Service(RegionEndpoint.EUWest2) :> IServices
            
        if event.Records = null then failwith "Records is null - exiting"

        let (deleteItems, addToDatabaseItems) =
            event.Records
            |> Seq.map
                (fun r ->
                    async {
                        let key = r.S3.Object.Key
                        let! imageHash = Reader.run (GetImageHashFromS3 BucketName key) services

                        let conditions =
                            ScanCondition("Hash", ScanOperator.Equal, imageHash :> obj)
                            |> Array.singleton

                        let hashFound =
                            Reader.run (GetTableContents conditions) services
                            |> (not << Seq.isEmpty)

                        return { S3Key = key; Hash = imageHash }, hashFound
                    })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.partition snd
            ||> fun d k -> Array.map fst d, Array.map fst k

        addToDatabaseItems
        |> Array.map
            (fun yl ->
                services.DynamoDBContext.SaveAsync<YeenLand>(yl.AsDynamo)
                |> Async.AwaitTask)
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        let deleteRequest = DeleteObjectsRequest(BucketName = BucketName)

        deleteItems
        |> Array.iter (fun yl -> deleteRequest.AddKey yl.S3Key)

        services.S3.DeleteObjectsAsync deleteRequest
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> ignore

    [<assembly: LambdaSerializer(typeof<DefaultLambdaJsonSerializer>)>]
    do ()
