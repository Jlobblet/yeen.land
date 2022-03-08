#r "nuget:AWSSDK.S3"
#r "nuget:Argu"
#r "nuget:Fake.IO.FileSystem"

open System
open System.IO
open Fake.IO.Globbing.Operators
open Argu
open Amazon
open Amazon.S3
open Amazon.S3.Transfer

// The first parameter is the filepath, so tail returns the rest of the arguments
let (name, argv) =
    let all = fsi.CommandLineArgs
    (Array.head all, Array.tail all)

type Arguments =
    | [<Unique>] BucketName of string
    | [<Unique>] RegionName of string
    | [<MainCommand; ExactlyOnce; Last>] Patterns of pattern: string list

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | BucketName _ -> "Bucket name to upload to"
            | RegionName _ -> "Region name the bucket is in"
            | Patterns _ -> "Glob patterns of files to upload"

let parser = ArgumentParser.Create<Arguments>(programName = name)

if argv |> Array.isEmpty then
    parser.PrintUsage() |> printfn "%s"
    exit 0

let parseRegionName s =
    let region = RegionEndpoint.GetBySystemName s

    match Seq.contains region RegionEndpoint.EnumerableAllRegions with
    | false -> failwith (region.ToString())
    | true -> region

let results = parser.Parse argv

let region =
    results.GetResult(<@ RegionName @>, defaultValue = "eu-west-2")
    |> parseRegionName

let bucketName = results.GetResult(<@ BucketName @>, defaultValue = "yeen.land")

let patterns = results.GetResult(<@ Patterns @>)

printfn $"Region: %A{region}"
printfn $"Bucket: %s{bucketName}"

let globs = List.fold (++) (!! "") patterns

let s3Client = new AmazonS3Client(region)

let uploadFile fp =
    printfn $"Uploading {fp}"
    let transfer = new TransferUtility(s3Client)

    TransferUtilityUploadRequest(
        BucketName = bucketName,
        FilePath = fp,
        StorageClass = S3StorageClass.Standard,
        CannedACL = S3CannedACL.PublicRead
    )
    |> transfer.UploadAsync
    |> Async.AwaitTask

globs
|> Seq.filter File.Exists
|> Seq.map uploadFile
|> Async.Parallel
|> Async.RunSynchronously
|> ignore

exit 0
