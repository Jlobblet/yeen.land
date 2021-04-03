namespace yeenland_homepage

open FSharpPlus.Data
open yeenland.yeenland
open Amazon
open Amazon.Lambda.Core
open Amazon.Lambda.APIGatewayEvents

module Function =
    let FunctionHandler (_: APIGatewayProxyRequest) (_: ILambdaContext) =
        let services =
            new Service(RegionEndpoint.EUWest2) :> IServices

        GenerateRandomPage ()
        |> Reader.run
        <| services
        |> Async.StartAsTask

    [<assembly:LambdaSerializer(typeof<Amazon.Lambda.Serialization.Json.JsonSerializer>)>]
    do ()
