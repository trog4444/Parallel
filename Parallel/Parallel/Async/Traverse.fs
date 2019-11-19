namespace Rogz.Parallel


/// <summary>Parallel computations via sequence-traversals.</summary>
module Traverse =

    module private Util =
        let inline unNil (xs: _ seq) = if isNull xs then Seq.empty else xs
        let inline setDegree n = max 1 n

    open Util


    /// <summary>Divides computations into chunks of length at most 'size', running the computations in parallel.</summary>
    let inline chunkBySize size (source: Async< ^a> seq) = async {
        let! xs = Seq.chunkBySize (setDegree size) (unNil source)
                  |> Seq.map Async.Parallel
                  |> Async.Parallel
        return Seq.concat xs }

    /// <summary>Splits computations into sequences of at most 'count' size, running the computations in parallel.</summary>
    let inline splitInto count (source: Async< ^a> seq) = async {        
        let! xs = Seq.splitInto (setDegree count) (unNil source)
                  |> Seq.map Async.Parallel
                  |> Async.Parallel
        return Seq.concat xs }

    /// <summary>Splits the input sequence into chunks of at most `count` size, projects an Async computation
    /// producing function across each sequence, and return a single Async comnputation that returns
    /// the final sequence of results.
    ///
    /// The computations are run in parallel, but only as many `chunks` as are needed are run, i.e.
    /// if the sequence is not consumed, none of the computations will be run, but if even 1 of
    /// member of a `chunk` is consumed, the entire `chunk` will be run.</summary>
    let inline traverse count projection (source: ^a seq) =
        //sequence count (Seq.map projection source)
        async { return seq {
            for chunk in Seq.chunkBySize (setDegree count) (unNil source) do
            for task in [| for x in chunk -> Async.RunSynchronously (Async.StartChild (projection x)) |] do
                yield Async.RunSynchronously task } }

    /// <summary>Splits the input sequence of Async computations into chunks of at most `count` size,
    /// and return a single Async comnputation that returns the final sequence of results.
    ///
    /// The computations are run in parallel, but only as many `chunks` as are needed are run, i.e.
    /// if the sequence is not consumed, none of the computations will be run, but if even 1 of
    /// member of a `chunk` is consumed, the entire `chunk` will be run.</summary>
    let inline sequence count source =
        //traverse count id source
        async { return seq {
            for chunk in Seq.chunkBySize (setDegree count) (unNil source) do
            for task in [| for x in chunk -> Async.RunSynchronously (Async.StartChild x) |] do
               yield Async.RunSynchronously task } }