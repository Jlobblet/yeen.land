namespace yeenland_permalink

open System
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
    let FunctionHandler (request: APIGatewayProxyRequest) (_: ILambdaContext) =
        let services = new Service(RegionEndpoint.EUWest2) :> IServices

        printfn $"%A{request.Path}"
        printfn $"%A{request.PathParameters}"

        request.PathParameters
        |> Seq.map (fun kvp -> kvp.Key, kvp.Value)
        |> Map.ofSeq
        |> Map.tryFind "id"
        |> Option.bind TryParseUInt64
        |> TryGetPageFromHash
        |> Reader.run
        <| services
        |> Async.RunSynchronously

    [<assembly: LambdaSerializer(typeof<DefaultLambdaJsonSerializer>)>]
    do ()
