namespace yeenland_api

open Amazon
open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents
open Amazon.Lambda.Serialization.SystemTextJson
open FSharpPlus.Data
open Newtonsoft.Json
open yeenland.DynamoDB
open yeenland.Services
open yeenland.Utils
open yeenland.yeenland

module Function =
    let private GenerateResponse (url: string option) =
        let headers =
            [ ("Content-Type", "application/json") ]
            |> ToDictionary

        let ``404`` =
            let body = [ "url", "" ] |> ToDictionary
            let statusCode = 404
            body, statusCode

        let body, statusCode =
            url
            |> Option.map (fun u ->
                let body = [ "url", u ] |> ToDictionary
                let statusCode = 200
                body, statusCode)
            |> Option.defaultValue ``404``

        async {
            return
                APIGatewayProxyResponse
                    (Body = JsonConvert.SerializeObject body, StatusCode = statusCode, Headers = headers)
        }
        |> Async.StartAsTask

    let FunctionHandler (request: APIGatewayProxyRequest) (_: ILambdaContext) =
        let services =
            new Service(RegionEndpoint.EUWest2) :> IServices

        let pathParameters =
            request.PathParameters
            |> Seq.map (fun kvp -> kvp.Key, kvp.Value)
            |> Map.ofSeq

        pathParameters
        |> Map.tryFind "id"
        |> Option.bind TryParseUInt64
        |> Option.map (
            TryGetRecordFromHash
            >> Reader.bind (Option.fold (fun _ -> Reader.Return<_, _>) (GetRandomRecord()))
            >> Reader.bind GetRecordUrl
            >> Reader.run
            >> fun f -> services |> f)
        |> GenerateResponse

    [<assembly: LambdaSerializer(typeof<DefaultLambdaJsonSerializer>)>]
    do ()
