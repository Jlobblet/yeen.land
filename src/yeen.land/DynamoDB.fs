module yeenland.DynamoDB

open Amazon.DynamoDBv2.DataModel
open FSharpPlus.Data
open yeenland.Services

[<Literal>]
let TableName = "yeen.land"

[<DynamoDBTable(TableName)>]
[<StructuredFormatDisplay("{AsString}")>]
type YeenLand() =
    [<DynamoDBHashKey>]
    member val S3Key: string = "" with get, set

    member val Hash: uint64 = 0uL with get, set

    member this.AsRecord = { S3Key = this.S3Key; Hash = this.Hash }
    static member FromRecord { S3Key = s3Key; Hash = hash } = YeenLand(S3Key = s3Key, Hash = hash)

    override this.ToString() = this.AsRecord.ToString()

    member this.AsString = this.ToString()

and YeenLandRecord =
    { S3Key: string
      Hash: uint64 }

    member this.AsDynamo =
        YeenLand(S3Key = this.S3Key, Hash = this.Hash)

    static member FromDynamo(yl: YeenLand) = { S3Key = yl.S3Key; Hash = yl.Hash }

let GetTableContents conditions =
    let inner (services: IServices) =
        services
            .DynamoDBContext
            .ScanAsync<YeenLand>(conditions)
            .GetRemainingAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> Seq.map YeenLandRecord.FromDynamo

    inner |> Reader
