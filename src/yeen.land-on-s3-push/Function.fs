namespace yeenland_on_s3_push

open Amazon
open Amazon.DynamoDBv2.DataModel
open Amazon.DynamoDBv2.DocumentModel
open Amazon.Lambda.Core
open Amazon.Lambda.S3Events
open Amazon.Lambda.Serialization.SystemTextJson
open Amazon.S3.Model
open FSharpPlus.Data
open yeenland
open yeenland.DynamoDB
open yeenland.S3
open yeenland.Services

module Function =

    let FunctionHandler (event: S3Event) (context: ILambdaContext) =
        let services =
            new Service(RegionEndpoint.EUWest2) :> IServices

        if event.Records = null then failwith "Records is null - exiting"

        let (deleteItems, addToDatabaseItems) =
            event.Records
            |> Seq.map (fun r ->
                async {
                    let key = r.S3.Object.Key
                    let! imageHash = Reader.run (GetImageHashFromS3 BucketName key) services

                    let conditions =
                        ScanCondition("Hash", ScanOperator.Equal, imageHash :> obj)
                        |> Array.singleton

                    let collisions =
                        Reader.run (GetTableContents conditions) services

                    return { S3Key = key; Hash = imageHash }, collisions
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.partition (snd >> Seq.isEmpty >> not)
            ||> fun d k -> d, Array.map fst k

        if not << Seq.isEmpty <| addToDatabaseItems then
            addToDatabaseItems
            |> Array.map (fun yl ->
                context.Logger.LogLine
                <| sprintf "Adding database entry %A" yl

                services.DynamoDBContext.SaveAsync<YeenLand>(yl.AsDynamo)
                |> Async.AwaitTask)
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore

        if not << Seq.isEmpty <| deleteItems then
            let deleteRequest =
                DeleteObjectsRequest(BucketName = BucketName)

            deleteItems
            |> Array.iter (fun (yl, cs) ->
                context.Logger.LogLine
                <| sprintf "Deleting object %s (collisions: %A)" yl.S3Key cs

                deleteRequest.AddKey yl.S3Key)

            services.S3.DeleteObjectsAsync deleteRequest
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> ignore

    [<assembly:LambdaSerializer(typeof<DefaultLambdaJsonSerializer>)>]
    do ()
