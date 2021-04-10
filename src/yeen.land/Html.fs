module yeenland.Html

open Giraffe.ViewEngine
open FSharpPlus.Data
open yeenland.DynamoDB

let css = """
body {
    font-family: sans-serif;
}
.imgbox {
    height: 100%;
    max-height: 100vh;
}
.center-fit {
    max-width: 100%;
    max-height: 100vh;
    margin: auto;
}
.center-text {
    height: 100px;
    line-height: 100px;
    text-align: center;
}
.inline-box {
    display: inline-block;
    vertical-align:middle;
    line-height:normal;
}
"""

let GeneratePage (yl: YeenLandRecord) =
    let inner services =
        let imgSrc = Reader.run (GetRecordUrl yl) services
        let permalink = $"https://yeen.land/id/%i{yl.Hash}"
        let random = "https://yeen.land"

        html [] [
            head [] [
                meta [ _charset "UTF-8" ]
                meta [ _property "og:type"
                       _content "website" ]
                meta [ _property "og:image"
                       _content imgSrc ]
                title [] [ str "yeen.land" ]
                style [] [
                    str css
                ]
            ]
            main [] [
                div [ _class "imgbox" ] [
                    img [ _class "center-fit"; _src imgSrc ]
                ]
                a [ _href permalink ] [
                    str "Permalink"
                ]
                str " "
                a [ _href random ] [ str "Random" ]
            ]
        ]

    inner |> Reader

let ``404 Page`` =
    let random = "https://yeen.land"
    html [] [
        head [] [
            title [] [ str "yeen.land" ]
            meta [ _property "og:type"
                   _content "website" ]
            style [] [
                str css
            ]
        ]
        main [] [
            div [ _class "center-text" ] [
                str "404"
            ]
        ]
        footer [] [
            a [ _href random ] [ str "Random" ]
        ]
    ]
    |> Reader.Return<_, _>

let TryGeneratePage: YeenLandRecord option -> Reader<Services.IServices,XmlNode> =
    Option.fold (fun _ -> GeneratePage) ``404 Page``
