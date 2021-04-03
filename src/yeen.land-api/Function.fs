namespace yeenland_api

open FSharpPlus.Data
open Newtonsoft.Json
open yeenland.yeenland
open Amazon
open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

module Function =
    let private GenerateResponse (url: string) =
        let body =
            [ "url", url ]
            |> dict
        let headers =
            [ ("Content-Type", "application/json") ]
            |> dict
        async {
            return APIGatewayProxyResponse(Body = JsonConvert.SerializeObject body, StatusCode = 200, Headers = headers)
        } |> Async.StartAsTask
    
    let FunctionHandler (_: APIGatewayProxyRequest) (_: ILambdaContext) =
        let services =
            new Service(RegionEndpoint.EUWest2) :> IServices

        GenerateRandomUrl ()
        |> Reader.run
        <| services
        |> GenerateResponse

    [<assembly:LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
    do ()
