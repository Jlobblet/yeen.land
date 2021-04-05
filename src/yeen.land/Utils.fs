module yeenland.Utils

open System.Collections.Generic

let ToDictionary (iEnumerable: ('Key * 'Value) seq) =
    iEnumerable
    |> dict
    |> Dictionary<'Key, 'Value>
