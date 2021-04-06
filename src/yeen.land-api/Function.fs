namespace yeenland_api

open Amazon
open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents
open Amazon.Lambda.Serialization.SystemTextJson
open FSharpPlus.Data
open Newtonsoft.Json
open yeenland.Services
open yeenland.Utils
open yeenland.yeenland

module Function =
    let private GenerateResponse (url: string) =
        let body = [ "url", url ] |> ToDictionary

        let headers =
            [ ("Content-Type", "application/json") ]
            |> ToDictionary

        async {
            return APIGatewayProxyResponse(Body = JsonConvert.SerializeObject body, StatusCode = 200, Headers = headers)
        }
        |> Async.StartAsTask

    let FunctionHandler (_: APIGatewayProxyRequest) (_: ILambdaContext) =
        let services =
            new Service(RegionEndpoint.EUWest2) :> IServices

        GetRandomRecord()
        |> Reader.bind GetRecordUrl
        |> Reader.run
        <| services
        |> GenerateResponse

    [<assembly: LambdaSerializer(typeof<DefaultLambdaJsonSerializer>)>]
    do ()
