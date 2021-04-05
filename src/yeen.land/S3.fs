module yeenland.S3

open Amazon.S3.Model
open FSharpPlus.Data
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
        sprintf "https://s3.%s.amazonaws.com/%s/%s" services.Region.SystemName bucketName objectKey

    inner |> Reader
