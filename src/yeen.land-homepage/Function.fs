namespace yeenland_homepage

open Amazon
open Amazon.Lambda.Core
open Amazon.Lambda.Serialization.SystemTextJson
open Amazon.Lambda.APIGatewayEvents
open FSharpPlus.Data
open yeenland.Services
open yeenland.yeenland

module Function =
    let FunctionHandler (_: APIGatewayProxyRequest) (_: ILambdaContext) =
        let services =
            new Service(RegionEndpoint.EUWest2) :> IServices

        GenerateRandomPage() |> Reader.run <| services
        |> Async.StartAsTask

    [<assembly:LambdaSerializer(typeof<DefaultLambdaJsonSerializer>)>]
    do ()
