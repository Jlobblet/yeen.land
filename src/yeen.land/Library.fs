module yeenland.yeenland

open System
open Amazon
open Amazon.DynamoDBv2
open Amazon.Lambda.APIGatewayEvents
open Amazon.S3
open Amazon.S3.Model
open FSharpPlus.Data
open Giraffe.ViewEngine

type IServices =
    inherit IDisposable
    abstract Region: RegionEndpoint
    abstract DynamoDB: IAmazonDynamoDB
    abstract S3: IAmazonS3
    abstract Random: Random

type Service(Region: RegionEndpoint) =
    interface IServices with
        member this.Region = Region
        member this.DynamoDB =
            new AmazonDynamoDBClient((this :> IServices).Region) :> IAmazonDynamoDB
        member this.S3 =
            new AmazonS3Client((this :> IServices).Region) :> IAmazonS3

        member this.Random = Random()

        member this.Dispose() = (this :> IServices).S3.Dispose()

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
        let s = Seq.unfold generator (Some request) |> Seq.concat
        // Reset the request to how we found it
        request.ContinuationToken <- tok
        s

    inner |> Reader

let GetRandom items =
    let inner (services: IServices) =
        items
        |> Seq.length
        |> services.Random.Next
        |> Seq.item
        <| items

    inner |> Reader

let GetRandomBucketItem request =
    GetBucketContents request |> Reader.bind GetRandom
    
let GetObjectUrl (request: ListObjectsV2Request) (object: S3Object) =
    let inner (services: IServices) =
        sprintf "https://s3.%s.amazonaws.com/%s/%s" services.Region.SystemName request.BucketName object.Key

    inner |> Reader
    
let GeneratePage imgSrc =
    html [] [
            header [] [
                title [] [ str "yeen.land" ]
                meta [ _property "og:type"
                       _content "website" ]
                meta [ _property "og:image"
                       _content imgSrc ]
                style [] [
                    str ".imgbox{height:100%}.center-fit{max-width:100%;max-height:100vh;margin:auto}"
                ]
            ]
            body [] [
                div [ _class "imgbox" ] [
                    img [ _class "center-fit"; _src imgSrc ]
                ]
            ]
        ]
    
let GenerateResponse (pageHtml: XmlNode) =
    let body = pageHtml |> RenderView.AsString.htmlDocument
    let headers =
        [ ("Content-Type", "text/html") ]
        |> dict
    async {
        return APIGatewayProxyResponse(Body = body, StatusCode = 200, Headers = headers)
    }
    
let GenerateRandomUrl () =
    let request =
        ListObjectsV2Request(BucketName = "yeen.land")
        
    request
    |> GetRandomBucketItem
    |> Reader.bind (GetObjectUrl request)
    
let GenerateRandomPage () =
    GenerateRandomUrl ()
    |> Reader.map GeneratePage
    |> Reader.map GenerateResponse

    
let main argv =
    let services =
        new Service(RegionEndpoint.EUWest2) :> IServices

    let request =
        ListObjectsV2Request(BucketName = "yeen.land")

    let key =
        GetRandomBucketItem request
        |> Reader.run
        <| services
        |> fun o -> o.Key

    let url =
        "https://s3.eu-west-2.amazonaws.com/yeen.land/"

    let yeen = sprintf "%s%s" url key

    let page =
        html [] [
            header [] [
                title [] [ str "yeen.land" ]
                meta [ _property "og:type"
                       _content "website" ]
                meta [ _property "og:image"
                       _content yeen ]
                style [] [
                    str ".imgbox{height:100%}.center-fit{max-width:100%;max-height:100vh;margin:auto}"
                ]
            ]
            body [] [
                div [ _class "imgbox" ] [
                    img [ _class "center-fit"; _src yeen ]
                ]
            ]
        ]

    page
