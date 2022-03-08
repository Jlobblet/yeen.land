module yeenland.yeenland

open Amazon.DynamoDBv2.DataModel
open Amazon.DynamoDBv2.DocumentModel
open Amazon.Lambda.APIGatewayEvents
open FSharpPlus.Data
open Giraffe.ViewEngine
open yeenland.DynamoDB
open yeenland.Html
open yeenland.Services
open yeenland.S3
open yeenland.Utils

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


let GenerateHtmlResponse (pageHtml: XmlNode) =
    let body = pageHtml |> RenderView.AsString.htmlDocument

    let headers = [ ("Content-Type", "text/html") ] |> ToDictionary

    async { return APIGatewayProxyResponse(Body = body, StatusCode = 200, Headers = headers) }

let TryGetRecordFromHash (_hash: uint64) =
    let conditions =
        ScanCondition("Hash", ScanOperator.Equal, _hash :> obj)
        |> Array.singleton

    conditions
    |> GetTableContents
    |> Reader.map Seq.tryHead

let GetRandomRecord () =
    let conditions = [||]

    conditions
    |> GetTableContents
    |> Reader.bind GetRandom

let GenerateRandomPage () =
    GetRandomRecord()
    |> Reader.bind GeneratePage
    |> Reader.map GenerateHtmlResponse

let GetPageFromHash =
    TryGetRecordFromHash
    >> Reader.bind TryGeneratePage
    >> Reader.map GenerateHtmlResponse

let TryGetPageFromHash: uint64 option -> Reader<IServices, Async<APIGatewayProxyResponse>> =
    Option.fold (fun _ -> GetPageFromHash) (``404 Page`` |> Reader.map GenerateHtmlResponse)
