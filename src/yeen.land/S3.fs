module yeenland.S3

open System.IO
open System.Threading
open Amazon.S3.Model
open FSharpPlus.Data
open SixLabors.ImageSharp
open yeenland.Services

[<Literal>]
let BucketName = "yeen.land"

let GetBucketContents (request: ListObjectsV2Request) =
    let inner (services: IServices) =
        let generator state =
            match state with
            | None -> None
            | Some request ->
                let response =
                    services.S3.ListObjectsV2Async request
                    |> Async.AwaitTask
                    |> Async.RunSynchronously

                match response.IsTruncated with
                | true ->
                    request.ContinuationToken <- response.NextContinuationToken
                    Some(response.S3Objects, Some request)
                | false -> Some(response.S3Objects, None)

        let tok = request.ContinuationToken

        let s =
            Seq.unfold generator (Some request) |> Seq.concat
        // Reset the request to how we found it
        request.ContinuationToken <- tok
        s

    inner |> Reader

let GetObjectUrl bucketName objectKey =
    let inner (services: IServices) =
        $"https://s3.%s{services.Region.SystemName}.amazonaws.com/%s{bucketName}/%s{objectKey}"

    inner |> Reader

let GetTempFilepath bucketName =
    Path.Combine(Path.GetTempPath(), bucketName, Path.GetTempFileName())

let DownloadFile bucket key =
    let inner (services: IServices) =
        async {
            let getRequest =
                GetObjectRequest(BucketName = bucket, Key = key)

            let! getResponse =
                services.S3.GetObjectAsync getRequest
                |> Async.AwaitTask

            let source = new CancellationTokenSource()
            let filepath = GetTempFilepath bucket

            do!
                getResponse.WriteResponseStreamToFileAsync(filepath, false, source.Token)
                |> Async.AwaitTask

            return filepath
        }

    inner |> Reader

let GetImageHash (filepath: string) =
    let inner (services: IServices) =
        async {
            use! image = Image.LoadAsync filepath |> Async.AwaitTask
            return services.ImageHashAlgorithm.Hash(image)
        }

    inner |> Reader

let GetImageHashFromS3 bucket key =
    let inner services =
        async {
            let! filepath = Reader.run (DownloadFile bucket key) services
            let! imageHash = Reader.run (GetImageHash filepath) services
            File.Delete filepath
            return imageHash
        }

    inner |> Reader
