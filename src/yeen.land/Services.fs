module yeenland.Services

open System
open Amazon
open Amazon.DynamoDBv2
open Amazon.DynamoDBv2.DataModel
open Amazon.S3
open CoenM.ImageHash
open CoenM.ImageHash.HashAlgorithms

type IServices =
    inherit IDisposable
    abstract Region : RegionEndpoint
    abstract DynamoDB : IAmazonDynamoDB
    abstract DynamoDBContext : IDynamoDBContext
    abstract S3 : IAmazonS3
    abstract ImageHashAlgorithm : IImageHash
    abstract Random : Random

type Service(Region: RegionEndpoint) =
    interface IServices with
        member this.Region = Region

        member this.DynamoDB =
            new AmazonDynamoDBClient((this :> IServices).Region) :> IAmazonDynamoDB

        member this.DynamoDBContext =
            new DynamoDBContext((this :> IServices).DynamoDB) :> IDynamoDBContext

        member this.S3 =
            new AmazonS3Client((this :> IServices).Region) :> IAmazonS3

        member this.ImageHashAlgorithm = new AverageHash() :> IImageHash

        member this.Random = Random()

        member this.Dispose() = (this :> IServices).S3.Dispose()
