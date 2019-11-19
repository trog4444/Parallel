namespace Rogz.Parallel.Async


/// <summary>Type synonymns for existing .NET types used in Async programming.</summary>
[<AutoOpen>]
module Type =

    /// <summary>Synonymn for a System.Threading.CancellationToken.</summary>
    type CancellationToken = System.Threading.CancellationToken

    /// <summary>Synonymn for System.Threading.CancellationTokenSource.</summary>
    type CancellationTokenSource = System.Threading.CancellationTokenSource

    /// <summary>Synonymn for System.Threading.CancellationTokenRegistration.</summary>
    type CancellationTokenRegistration = System.Threading.CancellationTokenRegistration

    /// <summary>Synonymn for System.Threading.Tasks.TaskCanceledException.</summary>
    type TaskCanceledException = System.Threading.Tasks.TaskCanceledException


open Type

/// <summary>Common functions on Async computations.</summary>
module Common =

    /// <summary>Begin a slow computation and a fast computation, and return the (still running)
    /// slow computation as well as the final result of the fast computation.</summary>
    let inline pEval (slow: Async< ^slow>) (fast: Async< ^fast>)
        : Async<struct (Async< ^slow> * ^fast)> =
        async { let! ts = Async.StartChild slow
                let! f = fast
                return struct (ts, f) }

    /// <summary>Begin two computations, and return the (possibly still running) computations as a pair.</summary>
    let inline pEvals t1 t2 : Async<struct (Async< ^a> * Async< ^b>)> =
        async { let! t1 = Async.StartChild t1
                let! t2 = Async.StartChild t2
                return struct (t1, t2) }

    /// <summary>Convert a standard computation into an asynchronous one.</summary>
    let inline compute computation input = async { return computation input }

    /// <summary>Creates an Async computation with the given timeout in milliseconds.</summary>
    let inline sleep millisecondsDelay = Async.Sleep (max 0 millisecondsDelay)

    
    /// <summary>Convert a computation which may yield a variety of types into a computation that yields one type-of value.</summary>
    [<Sealed; AbstractClass>]
    type Pick =

        /// <summary>Run a computation which may yield one of many types of values, apply the corresponding
        /// function to the result, and return the final value.</summary>
        static member inline Of2(computation, f1, f2) =
            async { match! computation with
                    | Choice1Of2 a -> return f1 a
                    | Choice2Of2 b -> return f2 b }

        /// <summary>Run a computation which may yield one of many types of values, apply the corresponding
        /// function to the result, and return the final value.</summary>
        static member inline Of2(computation, f1, f2) =
            async { match! computation with
                    | Choice1Of2 a -> return! f1 a
                    | Choice2Of2 b -> return! f2 b }

        /// <summary>Run a computation which may yield one of many types of values, apply the corresponding
        /// function to the result, and return the final value.</summary>
        static member inline Of3(computation, f1, f2, f3) =
            async { match! computation with
                    | Choice1Of3 a -> return f1 a
                    | Choice2Of3 b -> return f2 b
                    | Choice3Of3 c -> return f3 c }

        /// <summary>Run a computation which may yield one of many types of values, apply the corresponding
        /// function to the result, and return the final value.</summary>
        static member inline Of3(computation, f1, f2, f3) =
            async { match! computation with
                    | Choice1Of3 a -> return! f1 a
                    | Choice2Of3 b -> return! f2 b
                    | Choice3Of3 c -> return! f3 c }


    /// <summary>CancellationToken generation, cancellation, registration, and other methods.</summary>
    [<Sealed; AbstractClass>]
    type Token =

        /// <summary>Create a new CancellationTokenSource.</summary>
        static member inline Source () = new CancellationTokenSource()

        /// <summary>Create a new CancellationTokenSource that will cancel the given cancellation delay in milliseconds.</summary>
        static member inline Source (millisecondsDelay: int) = new CancellationTokenSource(millisecondsDelay)

        /// <summary>Create a new CancellationTokenSource that will cancel after the given cancellation delay TimeSpan.</summary>
        static member inline Source (delay: System.TimeSpan) = new CancellationTokenSource(delay)


        /// <summary>Cancel the given TokenSource immediately.</summary>
        static member inline Cancel (ts: CancellationTokenSource) = ts.Cancel()
                
        /// <summary>Cancel the given TokenSource after the given delay TimeSpan.</summary>
        static member inline Cancel (ts: CancellationTokenSource, millisecondsDelay: int) = ts.CancelAfter(millisecondsDelay)
        
        /// <summary>Cancel the given TokenSource after the given delay in milliseconds.</summary>
        static member inline Cancel (ts: CancellationTokenSource, delay: System.TimeSpan) = ts.CancelAfter(delay)
        

        /// <summary>Create a new token source and register the given callback to its token.</summary>
        static member inline Register (callback: unit -> unit) : struct (CancellationTokenSource * CancellationTokenRegistration) =
            let s = Token.Source() in struct (s, s.Token.Register(fun () -> callback ()))
        
        /// <summary>Create a new token source that will cancel after the given delay and register the given callback to its token.</summary>
        static member inline Register (callback: unit -> unit, millisecondsDelay: int)
            : struct (CancellationTokenSource * CancellationTokenRegistration) =
            let s = Token.Source(millisecondsDelay) in struct (s, s.Token.Register(fun () -> callback ()))
        
        /// <summary>Create a new token source that will cancel after the given delay and register the given callback to its token.</summary>
        static member inline Register (callback: unit -> unit, delay: System.TimeSpan)
            : struct (CancellationTokenSource * CancellationTokenRegistration) =
            let s = Token.Source(delay) in struct (s, s.Token.Register(fun () -> callback ()))


        /// <summary>Register all of the given callbacks to an existing TokenSource's token.</summary>
        static member inline Register (source: CancellationTokenSource, callbacks: (unit -> unit) seq)
            : CancellationTokenRegistration seq =
                Seq.map (fun c -> source.Token.Register(fun () -> c ())) callbacks
                

    /// <summary>Add features, such as continuations and error handlers, to existing Async computations.</summary>
    [<Sealed; AbstractClass>]
    type Enrich =
        
        /// <summary>Returns an Async computation with the continuations that are provided.
        ///
        /// The main operation is run in an Async.Catch computation, and the 
        /// value continuation and onExn continuation are run as applicable.
        ///
        /// The onCancel continuation is called if the default/global token is cancelled if no
        /// specific token is provided.
        ///
        /// The onExn continuation, if provided, is called if an exception occurs inside the Async.Catch
        /// computation OR the final computation. If no continuation is provided, any exn raised
        /// by the main computation is captured in the Async.Catch block and returned, while an exn raised
        /// in further continuations is simply rethrown.</summary>
        static member inline WithContinuations(computation
                                              , (onCancel: System.OperationCanceledException -> unit)
                                              , (?cancelToken: CancellationToken)
                                              , (?continuation: ^a -> unit)
                                              , (?onExn: exn -> unit)) = async {
                    let a = async { let! r = Async.Catch computation
                                    match r with
                                    | Choice1Of2 a -> match continuation with
                                                      | None -> ()
                                                      | Some c -> c a
                                                      return Ok a
                                    | Choice2Of2 e -> match onExn with
                                                      | None -> ()
                                                      | Some c -> c e
                                                      return Error e }
                    match cancelToken with
                    | None ->
                        do Async.StartWithContinuations(
                             computation = a
                           , continuation = ignore
                           , exceptionContinuation = fun e -> match onExn with None -> raise e | Some c -> c e
                           , cancellationContinuation = onCancel)
                    | Some tkn ->
                        do Async.StartWithContinuations(computation = a
                           , continuation = ignore
                           , exceptionContinuation = fun e -> match onExn with None -> raise e | Some c -> c e
                           , cancellationContinuation = onCancel
                           , cancellationToken = tkn)
                    return! a }

        /// <summary>Wrap an Async computation in an Async.Catch, and apply continuation functions on the result which is returned.</summary>
        static member inline WithFold(computation: Async< ^a>, onResult, onExn) : Async< ^b> =
            async { match! Async.Catch computation with
                    | Choice1Of2 a -> return onResult a
                    | Choice2Of2 e -> return onExn e }

        /// <summary>Wrap an Async computation in an Async.Catch, and apply continuation functions on the result which is returned.</summary>
        static member inline WithFold(computation: Async< ^a>, onResult, onExn) : Async< ^b> =
            async { match! Async.Catch computation with
                    | Choice1Of2 a -> return! onResult a
                    | Choice2Of2 e -> return! onExn e }

        /// <summary>Wrap an Async computation in an Async.Choice, and apply continuation functions on the result which is returned.</summary>
        static member inline WithFold(computations: Async< ^a option> seq, onSome, state) : Async< ^s> =
            async { match! Async.Choice computations with
                    | Some a -> return onSome state a
                    | None   -> return state }

        /// <summary>Wrap an Async computation in an Async.Choice, and apply continuation functions on the result which is returned.</summary>
        static member inline WithFold(computations: Async< ^a option> seq, onSome, state) : Async< ^s> =
            async { match! Async.Choice computations with
                    | Some a -> return! onSome state a
                    | None   -> return state }