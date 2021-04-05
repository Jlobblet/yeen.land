module yeenland.yeenland

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
