#r "nuget:AWSSDK.DynamoDBv2"
#r "nuget:AWSSDK.S3"
#r "nuget:Argu"
#r "nuget:FSharpPlus"
#r "nuget:SixLabors.ImageSharp"
#r "nuget:CoenM.ImageSharp.ImageHash"
#r "../src/yeen.land/bin/Release/netcoreapp3.1/yeen.land.dll"

open System.IO
open System.Threading
open Argu
open Amazon
open Amazon.DynamoDBv2.DataModel
open Amazon.DynamoDBv2.DocumentModel
open Amazon.S3.Model
open FSharpPlus.Data
open SixLabors.ImageSharp
open CoenM.ImageHash
open CoenM.ImageHash.HashAlgorithms
open yeenland

// Constants
[<Literal>]
let tableName = "yeen.land"

// Parsing

// The first parameter is the filepath, so tail returns the rest of the arguments
let (name, argv) =
    let all = fsi.CommandLineArgs
    (Array.head all, Array.tail all)

type Arguments =
    | [<Unique>] BucketName of string
    | [<Unique>] RegionName of string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | BucketName _ -> "Bucket name to update table from"
            | RegionName _ -> "Region name the bucket is in"

let parser =
    ArgumentParser.Create<Arguments>(programName = name)

let parseRegionName s =
    let region = RegionEndpoint.GetBySystemName s

    match Seq.contains region RegionEndpoint.EnumerableAllRegions with
    | false -> failwith (region.ToString())
    | true -> region

let results = parser.Parse argv

let region =
    results.GetResult(<@ RegionName @>, defaultValue = "eu-west-2")
    |> parseRegionName

let bucketName =
    results.GetResult(<@ BucketName @>, defaultValue = "yeen.land")

printfn $"Region: %A{region}"
printfn $"Table name: %s{tableName}"
printfn $"Bucket name: %s{bucketName}"

// Variables

[<DynamoDBTable("yeen.land")>]
[<StructuredFormatDisplay("{AsString}")>]
type YeenLand() =
    [<DynamoDBHashKey>]
    member val S3Key: string = "" with get, set

    member val Hash: uint64 = 0uL with get, set

    member this.AsRecord = { S3Key = this.S3Key; Hash = this.Hash }
    static member FromRecord { S3Key = s3Key; Hash = hash } = YeenLand(S3Key = s3Key, Hash = hash)

    override this.ToString() = this.AsRecord.ToString()

    member this.AsString = this.ToString()

and YeenLandRecord =
    { S3Key: string
      Hash: uint64 }

    member this.AsDynamo =
        YeenLand(S3Key = this.S3Key, Hash = this.Hash)

    static member FromDynamo(yl: YeenLand) = { S3Key = yl.S3Key; Hash = yl.Hash }


let services =
    new yeenland.Service(region) :> yeenland.IServices

let context = new DynamoDBContext(services.DynamoDB)

let hashAlgorithm = AverageHash() :> IImageHash

let bucketContents =
    ListObjectsV2Request(BucketName = bucketName)
    |> yeenland.GetBucketContents
    |> Reader.run
    <| services

let tableContents =
    let search = context.ScanAsync<YeenLand>([])

    search.GetRemainingAsync()
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> Seq.map YeenLandRecord.FromDynamo

let bucketKeys =
    bucketContents
    |> Seq.map (fun o -> o.Key)
    |> Set.ofSeq

let presentTableKeys =
    tableContents
    |> Seq.map (fun d -> d.S3Key)
    |> Set.ofSeq

let keysToAdd =
    Set.difference bucketKeys presentTableKeys

let keysToUpdate =
    Set.intersect bucketKeys presentTableKeys

let keysToRemove =
    Set.difference presentTableKeys bucketKeys

printfn $"Keys to add   : %i{keysToAdd.Count}"
printfn $"Keys to update: %i{keysToUpdate.Count}"
printfn $"Keys to remove: %i{keysToRemove.Count}"

// Helper functions

let getTempFilepath () =
    Path.Combine(Path.GetTempPath(), bucketName, Path.GetTempFileName())

let downloadImage bucket key =
    async {
        let getRequest =
            new GetObjectRequest(BucketName = bucket, Key = key)

        let! getResponse =
            services.S3.GetObjectAsync getRequest
            |> Async.AwaitTask

        let source = new CancellationTokenSource()
        let filepath = getTempFilepath ()

        do! getResponse.WriteResponseStreamToFileAsync(filepath, false, source.Token)
            |> Async.AwaitTask

        return filepath
    }

let getImageHash (alg: IImageHash) (filepath: string) =
    async {
        use! image = Image.LoadAsync filepath |> Async.AwaitTask
        return alg.Hash(image)
    }

let getImageHashFromS3 bucket key alg =
    async {
        let! filepath = downloadImage bucket key
        let! imageHash = getImageHash alg filepath
        File.Delete filepath
        return imageHash
    }

// Main body

// Adding entries

bucketContents
|> Seq.filter (fun object -> keysToAdd.Contains object.Key)
|> Seq.map (fun object ->
    async {
        let! imageHash = getImageHashFromS3 bucketName object.Key hashAlgorithm

        let entry =
            { S3Key = object.Key; Hash = imageHash }.AsDynamo

        printfn $"Adding %A{entry}"

        return!
            context.SaveAsync<YeenLand>(entry)
            |> Async.AwaitTask
    })
|> Async.Parallel
|> Async.RunSynchronously

// Updating entries

let expectedEntries =
    bucketContents
    |> Seq.filter (fun object -> keysToUpdate.Contains object.Key)
    |> Seq.map (fun object ->
        async {
            let! imageHash = getImageHashFromS3 bucketName object.Key hashAlgorithm
            return object.Key, imageHash
        })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> Map.ofArray

let currentEntries =
    let conditions =
        ScanCondition
            ("S3Key",
             ScanOperator.In,
             values = (keysToUpdate
                       |> Seq.map (fun k -> k :> obj)
                       |> Array.ofSeq))

    context
        .ScanAsync<YeenLand>([| conditions |])
        .GetRemainingAsync()
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> Seq.map (fun yl -> yl.S3Key, yl.Hash)
    |> Map.ofSeq

expectedEntries
|> Map.filter (fun k v -> currentEntries.[k] <> v)
|> Seq.map (fun kvp ->
    let entry =
        { S3Key = kvp.Key; Hash = kvp.Value }.AsDynamo

    printfn $"Updating %A{entry}"

    context.SaveAsync<YeenLand>(entry)
    |> Async.AwaitTask)
|> Async.Parallel
|> Async.RunSynchronously

// Removing entries

keysToRemove
|> Seq.map (fun k ->
    printfn $"Deleting entry with key %s{k}"

    context.DeleteAsync<YeenLand>(k)
    |> Async.AwaitTask)
|> Async.Parallel
|> Async.RunSynchronously
