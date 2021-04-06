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

let GetRecordUrl yl = GetObjectUrl BucketName yl.S3Key

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

let GeneratePage (yl: YeenLandRecord) =
    let inner services =
        let imgSrc = Reader.run (GetRecordUrl yl) services
        let permalink = $"https://yeen.land/id/%i{yl.Hash}"
        let random = "https://yeen.land"

        html [] [
            header [] [
                title [] [ str "yeen.land" ]
                meta [ _property "og:type"
                       _content "website" ]
                meta [ _property "og:image"
                       _content imgSrc ]
                style [] [
                    str "body{font-family:sans-serif}.imgbox{height:100%}.center-fit{max-width:100%;max-height:100vh;margin:auto}"
                ]
            ]
            main [] [
                body [] [
                    div [ _class "imgbox" ] [
                        img [ _class "center-fit"; _src imgSrc ]
                    ]
                ]
            ]
            footer [] [
                body [] [
                    a [ _href permalink ] [
                        str "Permalink"
                    ]
                    str " "
                    a [ _href random ] [ str "Random" ]
                ]
            ]
        ]

    inner |> Reader

let ``404 Page`` =
    let random = "https://yeen.land"
    html [] [
        header [] [
            title [] [ str "yeen.land" ]
            meta [ _property "og:type"
                   _content "website" ]
            style [] [
                str
                    "body{font-family:sans-serif}.center-text{height:100px;line-height:100px;text-align:center;}.inline-box{display:inline-block;vertical-align:middle;line-height:normal}"
            ]
        ]
        main [] [
            body [] [
                div [ _class "center-text" ] [
                    str "404"
                ]
            ]
        ]
        footer [] [
            body [] [
                a [ _href random ] [ str "Random" ]
            ]
        ]
    ]
    |> Reader.Return<_, _>

let TryGeneratePage =
    Option.fold (fun _ -> GeneratePage) ``404 Page``

let GenerateHtmlResponse (pageHtml: XmlNode) =
    let body =
        pageHtml |> RenderView.AsString.htmlDocument

    let headers =
        [ ("Content-Type", "text/html") ] |> ToDictionary

    async { return APIGatewayProxyResponse(Body = body, StatusCode = 200, Headers = headers) }

let GetRandomRecord () =
    let conditions = [||]

    conditions
    |> GetTableContents
    |> Reader.bind GetRandom

let GenerateRandomPage () =
    GetRandomRecord()
    |> Reader.bind GeneratePage
    |> Reader.map GenerateHtmlResponse

let GetPageFromHash (_hash: uint64) =
    let conditions =
        ScanCondition("Hash", ScanOperator.Equal, _hash :> obj)
        |> Array.singleton

    conditions
    |> GetTableContents
    |> Reader.map Seq.tryHead
    |> Reader.bind TryGeneratePage
    |> Reader.map GenerateHtmlResponse

let TryGetPageFromHash: uint64 option -> Reader<IServices, Async<APIGatewayProxyResponse>> =
    Option.fold (fun _ -> GetPageFromHash) (``404 Page`` |> Reader.map GenerateHtmlResponse)
