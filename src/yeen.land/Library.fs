module yeenland.yeenland

open Amazon.DynamoDBv2.DataModel
open Amazon.DynamoDBv2.DocumentModel
open Amazon.Lambda.APIGatewayEvents
open FSharpPlus.Data
open Giraffe.ViewEngine
open yeenland.DynamoDB
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

let ``404 Page`` =
    html [] [
        header [] [
            title [] [ str "yeen.land" ]
            meta [ _property "og:type"
                   _content "website" ]
            style [] [
                str
                    ".center-text{height:100px;line-height:100px;text-align:center;}.inline-box{display:inline-block;vertical-align:middle;line-height:normal}"
            ]
        ]
        body [] [
            div [ _class "center-text" ] [
                str "404"
            ]
        ]
    ]

let ``404 Page Reader``: Reader<IServices, XmlNode> = ``404 Page`` |> Reader.Return<_, _>

let GenerateHtmlResponse (pageHtml: XmlNode) =
    let body =
        pageHtml |> RenderView.AsString.htmlDocument

    let headers =
        [ ("Content-Type", "text/html") ] |> ToDictionary

    async { return APIGatewayProxyResponse(Body = body, StatusCode = 200, Headers = headers) }

let GenerateRandomUrl () =
    let conditions = [||]

    conditions
    |> GetTableContents
    |> Reader.bind GetRandom
    |> Reader.bind (fun yl -> GetObjectUrl BucketName yl.S3Key)

let GenerateRandomPage () =
    GenerateRandomUrl()
    |> Reader.map GeneratePage
    |> Reader.map GenerateHtmlResponse

let GetPageFromHash (_hash: uint64) =
    let conditions =
        ScanCondition("Hash", ScanOperator.Equal, _hash :> obj)
        |> Array.singleton

    conditions
    |> GetTableContents
    |> Reader.map Seq.tryHead
    |> Reader.bind (function
        | Some yl ->
            yl.S3Key
            |> GetObjectUrl BucketName
            |> Reader.map GeneratePage
        | None -> ``404 Page Reader``)
    |> Reader.map GenerateHtmlResponse

let TryGetPageFromHash =
    Option.fold (fun _ -> GetPageFromHash)
        (``404 Page Reader``
         |> Reader.map GenerateHtmlResponse)
