namespace Rogz.Parallel.Async


/// <summary>Compositional operations on Async objects.</summary>
module Compose =

    /// <summary>Lift a value into an Async computation.</summary>
    let inline unit x = async.Return x

    /// <summary>Runs an Async computation and returns a new Async computation by applying a function to the result of the first.</summary>
    let inline bind k m = async.Bind(m, k)

    /// Run one level of a nested Async computation to return the inner Async.</summary>
    let inline flatten (mm: Async<Async< ^a>>) : Async< ^a> = async { let! m = mm in return! m }

    /// <summary>Run two Async computations in parallel -- one containing a value and the other containing
    /// a function that, when yielded, is applied to the value and the result is returned.</summary>
    let inline ap fv ff = async {
        let! f = Async.StartChild ff
        let! v = Async.StartChild fv
        let! f = f
        let! v = v
        return f v }

    /// <summary>Run an Async computation and apply a function to its result.</summary></summary>
    let inline map f m = async { let! a = m in return f a }


    /// <summary>Runs two Async computations in parallel and returns a new Async computation by applying a function to the result of the first two.</summary>
    let inline bind2 k m n = async {
        let! m = Async.StartChild m
        let! n = Async.StartChild n
        let! a = m
        let! b = n
        return! k a b }

    /// <summary>Runs three Async computations in parallel and returns a new Async computation by applying a function to the result of the first three.</summary>
    let inline bind3 k t1 t2 t3 = async {
        let! a = Async.StartChild t1
        let! b = Async.StartChild t2
        let! c = Async.StartChild t3
        let! a = a
        let! b = b
        let! c = c
        return! k a b c }

    
    /// <summary>Run two Async computations in parallel and apply a function to their results.</summary>
    let inline map2 f t1 t2 = async {
        let! a = Async.StartChild t1
        let! b = Async.StartChild t2
        let! a = a
        let! b = b
        return f a b }

    /// <summary>Run three Async computations in parallel and apply a function to their results.</summary>
    let inline map3 f t1 t2 t3 = async {
        let! a = Async.StartChild t1
        let! b = Async.StartChild t2
        let! c = Async.StartChild t3
        let! a = a
        let! b = b
        let! c = c
        return f a b c }


    /// <summary>Runs a blocking Async computation and returns its result (synonymn for Async.RunSynchronously).
    ///
    /// Warning -- this should be used SPARINGLY as it blocks the main thread.</summary>
    let inline extract w = Async.RunSynchronously w

    /// <summary>Begin an Async computation using Async.StartChild, and return the result
    /// of applying a function to the resulting handle to that computation.</summary>
    let inline extend (j: Async< ^a> -> ^b) w =
        async { let! w' = Async.StartChild w in return j w' }

    /// <summary>Create an Async computation that, when evaluated, returns a handle to a running Async computation (Synonymn for Async.StartChild).</summary>
    let inline duplicate w = Async.StartChild w