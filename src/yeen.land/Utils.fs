module yeenland.Utils

open System.Collections.Generic

let ToDictionary (iEnumerable: ('Key * 'Value) seq) =
    iEnumerable |> dict |> Dictionary<'Key, 'Value>

let TryParseUInt64 (str: string) =
    match System.UInt64.TryParse str with
    | true, i -> Some i
    | false, _ -> None
