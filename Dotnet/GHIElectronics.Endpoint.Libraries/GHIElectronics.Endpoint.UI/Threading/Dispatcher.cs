////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Microsoft Corporation.  All rights reserved.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

//In what case was EnsureStatics needed?
//When a program has a static Window type that gets initialized in the static constructor?
//Does this case still work or not? If not, keep code as it was but inlined.

namespace GHIElectronics.Endpoint.UI.Threading {
    /// <summary>
    ///     Provides UI services for a thread.
    /// </summary>
    public sealed class Dispatcher {
        /// <summary>
        /// Returns the Dispatcher for the current thread.
        /// </summary>
        /// <value>Dispatcher</value>
        public static Dispatcher CurrentDispatcher {
            get {
                var dispatcher = FromThread(Thread.CurrentThread);

                //While FromThread() and Dispatcher() both operate in the GlobalLock,
                //and this function does not, there is no race condition because threads cannot
                //create Dispatchers on behalf of other threads. Thus, while other threads may
                //create a Dispatcher for themselves, they cannot create a Dispatcher for this
                //thread, therefore only one Dispatcher for each thread can exist in the ArrayList,
                //and there is no race condition.
                if (dispatcher == null) {
                    lock (typeof(GlobalLock)) {
                        dispatcher = FromThread(Thread.CurrentThread);

                        dispatcher ??= new Dispatcher();
                    }
                }

                return dispatcher;
            }
        }

        /// <summary>
        ///     Returns the Dispatcher for the specified thread.
        /// </summary>
        /// <remarks>
        ///     If there is no dispatcher available for the specified thread,
        ///     this method will return null.
        /// </remarks>
        public static Dispatcher FromThread(Thread thread) {
            Dispatcher dispatcher = null;

            // _possibleDispatcher is initialized in the static constructor and is never changed.
            // According to section 12.6.6 of Partition I of ECMA 335, reads and writes of object
            // references shall be atomic.
            dispatcher = _possibleDispatcher;
            if (dispatcher == null || dispatcher._thread != thread) {
                // The "possible" dispatcher either was null or belongs to
                // the a different thread.
                dispatcher = null;

                var wref = (WeakReference)_dispatchers[thread.ManagedThreadId];

                if (wref != null) {
                    dispatcher = wref.Target as Dispatcher;
                    if (dispatcher != null) {
                        if (dispatcher._thread == thread) {
                            // Shortcut: we track one static reference to the last current
                            // dispatcher we gave out.  For single-threaded apps, this will
                            // be set all the time.  For multi-threaded apps, this will be
                            // set for periods of time during which accessing CurrentDispatcher
                            // is cheap.  When a thread switch happens, the next call to
                            // CurrentDispatcher is expensive, but then the rest are fast
                            // again.
                            _possibleDispatcher = dispatcher;
                        }
                        else {
                            _dispatchers.Remove(thread.ManagedThreadId);
                        }
                    }
                }
            }

            return dispatcher;
        }

        private Dispatcher() {
            this._thread = Thread.CurrentThread;
            this._queue = new Queue();
            this._event = new AutoResetEvent(false);
            this._instanceLock = new object();

            // Add ourselves to the map of dispatchers to threads.
            _dispatchers[this._thread.ManagedThreadId] = new WeakReference(this);

            _possibleDispatcher ??= this;
        }

        /// <summary>
        ///     Checks that the calling thread has access to this object.
        /// </summary>
        /// <remarks>
        ///     Only the dispatcher thread may access DispatcherObjects.
        ///     <p/>
        ///     This method is public so that any thread can probe to
        ///     see if it has access to the DispatcherObject.
        /// </remarks>
        /// <returns>
        ///     True if the calling thread has access to this object.
        /// </returns>
        public bool CheckAccess() => (this._thread == Thread.CurrentThread);

        /// <summary>
        ///     Verifies that the calling thread has access to this object.
        /// </summary>
        /// <remarks>
        ///     Only the dispatcher thread may access DispatcherObjects.
        ///     <p/>
        ///     This method is public so that derived classes can probe to
        ///     see if the calling thread has access to itself.
        /// </remarks>
        public void VerifyAccess() {
            if (this._thread != Thread.CurrentThread) {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Thread for the dispatcher.
        /// </summary>
        /// <value></value>
        public Thread Thread => this._thread;

        // Returns whether or not the operation was removed.
        internal bool Abort(DispatcherOperation operation) {
            var notify = false;

            lock (this._instanceLock) {
                if (operation.Status == DispatcherOperationStatus.Pending) {
                    operation.Status = DispatcherOperationStatus.Aborted;
                    notify = true;
                }
            }

            return notify;
        }

        /// <summary>
        ///     Begins the process of shutting down the dispatcher, synchronously.
        ///     The process may complete asynchronously, since we may be
        ///     nested in dispatcher frames.
        /// </summary>
        public void InvokeShutdown() {
            this.VerifyAccess();

            if (this._hasShutdownFinished) {
                throw new InvalidOperationException();
            }

            try {
                if (!this._hasShutdownStarted) {
                    // Call the ShutdownStarted event before we actually mark ourselves
                    // as shutting down.  This is so the handlers can actaully do work
                    // when they get this event without throwing exceptions.
                    ShutdownStarted?.Invoke(this, EventArgs.Empty);

                    this._hasShutdownStarted = true;

                    if (this._frameDepth > 0) {
                        // If there are any frames running, we have to wait for them
                        // to unwind before we can safely destroy the dispatcher.
                    }
                    else {
                        // The current thread is not spinning inside of the Dispatcher,
                        // so we can go ahead and destroy it.
                        this.ShutdownImpl();
                    }

                    _dispatchers.Remove(this._thread.ManagedThreadId);
                }
            }
            catch (Exception e) {
                if (this._finalExceptionHandler == null || !this._finalExceptionHandler(this, e)) {
                    throw;
                }
            }
        }

        private void ShutdownImpl() {
            Debug.Assert(this._hasShutdownStarted);
            Debug.Assert(!this._hasShutdownFinished);

            // Call the ShutdownFinished event before we actually mark ourselves
            // as shut down.  This is so the handlers can actaully do work
            // when they get this event without throwing exceptions.
            ShutdownFinished?.Invoke(this, EventArgs.Empty);

            // Mark this dispatcher as shut down.  Attempts to BeginInvoke
            // or Invoke will result in an exception.
            this._hasShutdownFinished = true;

            lock (this._instanceLock) {
                // Now that the queue is off-line, abort all pending operations,
                // including inactive ones.
                while (this._queue.Count > 0) {
                    var operation = (DispatcherOperation)this._queue.Dequeue();

                    operation?.Abort();
                }
            }
        }

        //
        // wakes up the dispatcher to force it to check the
        // frame.Continue flag
        internal void QueryContinueFrame() => this._event.Set();

        /// <summary>
        ///     Whether or not the dispatcher is shutting down.
        /// </summary>
        public bool HasShutdownStarted => this._hasShutdownStarted;

        /// <summary>
        ///     Whether or not the dispatcher has been shut down.
        /// </summary>
        public bool HasShutdownFinished => this._hasShutdownFinished;

        /// <summary>
        ///     Raised when the dispatcher starts shutting down.
        /// </summary>
        public event EventHandler ShutdownStarted;

        /// <summary>
        ///     Raised when the dispatcher is shut down.
        /// </summary>
        public event EventHandler ShutdownFinished;

        /// <summary>
        ///     Push the main execution frame.
        /// </summary>
        /// <remarks>
        ///     This frame will continue until the dispatcher is shut down.
        /// </remarks>
        public static void Run() => PushFrame(new DispatcherFrame());

        /// <summary>
        ///     Push an execution frame.
        /// </summary>
        /// <param name="frame">
        ///     The frame for the dispatcher to process.
        /// </param>
        public static void PushFrame(DispatcherFrame frame) {
            if (frame == null) {
                throw new ArgumentNullException();
            }

            var dispatcher = Dispatcher.CurrentDispatcher;
            if (dispatcher._hasShutdownFinished) {
                throw new InvalidOperationException();
            }

            dispatcher.PushFrameImpl(frame);
        }

        internal DispatcherFrame CurrentFrame => this._currentFrame;

        //
        // instance implementation of PushFrame
        private void PushFrameImpl(DispatcherFrame frame) {
            var prevFrame = this._currentFrame;
            this._frameDepth++;
            try {
                this._currentFrame = frame;

                while (frame.Continue) {
                    DispatcherOperation op = null;
                    var aborted = false;

                    //
                    // Dequeue the next operation if appropriate
                    if (this._queue.Count > 0) {
                        op = (DispatcherOperation)this._queue.Dequeue();

                        //Must check aborted flag inside lock because
                        //user program could call op.Abort() between
                        //here and before the call to Invoke()
                        aborted = op.Status == DispatcherOperationStatus.Aborted;
                    }

                    if (op != null) {
                        if (!aborted) {
                            // Invoke the operation:
                            Debug.Assert(op._status == DispatcherOperationStatus.Pending);

                            // Mark this operation as executing.
                            op._status = DispatcherOperationStatus.Executing;

                            op._result = null;

                            try {
                                op._result = op._method(op._args);
                            }
                            catch (Exception e) {
                                if (this._finalExceptionHandler == null ||
                                        !this._finalExceptionHandler(op, e)) {
                                    throw;
                                }
                            }

                            // Mark this operation as completed.
                            op._status = DispatcherOperationStatus.Completed;

                            // Raise the Completed so anyone who is waiting will wake up.
                            op.OnCompleted();
                        }
                    }
                    else {
                        this._event.WaitOne();
                    }
                }
            }
            finally {
                this._frameDepth--;

                this._currentFrame = prevFrame;

                // If this was the last frame to exit after a quit, we
                // can now dispose the dispatcher.
                if (this._frameDepth == 0) {
                    if (this._hasShutdownStarted) {
                        this.ShutdownImpl();
                    }
                }
            }
        }

        /// <summary>
        ///     Executes the specified delegate asynchronously with the specified
        ///     arguments, on the thread that the Dispatcher was created on.
        /// </summary>
        /// <param name="method">
        ///     A delegate to a method that takes parameters of the same number
        ///     and type that are contained in the args parameter.
        /// </param>
        /// <param name="args">
        ///     An object to pass as the argument to the given method.
        ///     This can be null if no arguments are needed.
        /// </param>
        /// <returns>
        ///     A DispatcherOperation object that represents the result of the
        ///     BeginInvoke operation.  null if the operation could not be queued.
        /// </returns>
        public DispatcherOperation BeginInvoke(DispatcherOperationCallback method, object args) {
            if (method == null) {
                throw new ArgumentNullException();
            }

            DispatcherOperation operation = null;

            if (!this._hasShutdownFinished) {
                operation = new DispatcherOperation(this, method, args);

                // Add the operation to the work queue
                this._queue.Enqueue(operation);

                // this will only cause at most 1 extra dispatcher loop, so
                // always set the event.
                this._event.Set();
            }

            return operation;
        }

        /// <summary>
        ///     Executes the specified delegate synchronously with the specified
        ///     arguments, on the thread that the Dispatcher was created on.
        /// </summary>
        /// <param name="timeout">
        ///     The maximum amount of time to wait for the operation to complete.
        /// </param>
        /// <param name="method">
        ///     A delegate to a method that takes parameters of the same number
        ///     and type that are contained in the args parameter.
        /// </param>
        /// <param name="args">
        ///     An object to pass as the argument to the given method.
        ///     This can be null if no arguments are needed.
        /// </param>
        /// <returns>
        ///     The return value from the delegate being invoked, or null if
        ///     the delegate has no return value or if the operation was aborted.
        /// </returns>
        public object Invoke(TimeSpan timeout, DispatcherOperationCallback method, object args) {
            if (method == null) {
                throw new ArgumentNullException();
            }

            object result = null;

            var op = this.BeginInvoke(method, args);

            if (op != null) {
                op.Wait(timeout);

                if (op.Status == DispatcherOperationStatus.Completed) {
                    result = op.Result;
                }
                else if (op.Status == DispatcherOperationStatus.Aborted) {
                    // Hm, someone aborted us.  Maybe the dispatcher got
                    // shut down on us?  Just return null.
                }
                else {
                    // We timed out, just abort the op so that it doesn't
                    // invoke later.
                    //
                    // Note the race condition: if this is a foreign thread,
                    // it is possible that the dispatcher thread could actually
                    // dispatch the operation between the time our Wait()
                    // call returns and we get here.  In the case the operation
                    // will actually be dispatched, but we will return failure.
                    //
                    // We recognize this but decide not to do anything about it,
                    // as this is a common problem is multi-threaded programming.
                    op.Abort();
                }
            }

            return result;
        }

        //
        // Invoke a delegate in a try/catch.
        //
        internal object WrappedInvoke(DispatcherOperationCallback callback, object arg) {
            object result = null;

            try {
                result = callback(arg);
            }
            catch (Exception e) {
#if TINYCLR_DEBUG_DISPATCHER
                // allow the debugger to break on the original exception.
                if (System.Diagnostics.Debugger.IsAttached)
                {
                }
                else
#endif
                if (this._finalExceptionHandler == null || !this._finalExceptionHandler(this, e)) {
                    throw;
                }
            }

            return result;
        }

        internal static void SetFinalDispatcherExceptionHandler(DispatcherExceptionEventHandler handler) => Dispatcher.CurrentDispatcher.SetFinalExceptionHandler(handler);

        internal void SetFinalExceptionHandler(DispatcherExceptionEventHandler handler) => this._finalExceptionHandler = handler;

        private DispatcherFrame _currentFrame;
        private int _frameDepth;
        internal bool _hasShutdownStarted;  // used from DispatcherFrame
        private bool _hasShutdownFinished;

        private Queue _queue;
        private AutoResetEvent _event;
        private object _instanceLock;

        static Hashtable _dispatchers = new Hashtable();
        static Dispatcher _possibleDispatcher;

        // note: avalon uses a weakreference to track the thread.  the advantage i can see to that
        // is in case some other thread has a reference to the dispatcher object, but the dispatcher thread
        // has terminated.  In that case the Thread object would remain until the Dispatcher is GC'd.
        // we dont' have much unmanaged state associated with a dead thread, so it's probably okay to let it
        // hang around.   if we need to run a finalizer on the thread or something, then we should use a weakreference here.

        private Thread _thread;

        // Raised when a dispatcher exception was caught during an Invoke or BeginInvoke
        // Hooked in by the application.
        internal DispatcherExceptionEventHandler _finalExceptionHandler;

        // these are per dispatcher, track them here.
        internal GHIElectronics.Endpoint.UI.LayoutManager _layoutManager;
        internal GHIElectronics.Endpoint.UI.Input.InputManager _inputManager;
        internal GHIElectronics.Endpoint.UI.Media.MediaContext _mediaContext;

        //
        // we use this type of a global static lock.  we can't guarantee
        // static constructors are run int he right order, but we can guarantee the
        // lock for the type exists.
        class GlobalLock { }
    }

    /// <summary>
    ///   Delegate for processing exceptions that happen during Invoke or BeginInvoke.
    ///   Return true if the exception was processed.
    /// </summary>
    internal delegate bool DispatcherExceptionEventHandler(object sender, Exception e);

    /// <summary>
    ///   A convenient delegate to use for dispatcher operations.
    /// </summary>
    public delegate object DispatcherOperationCallback(object arg);

}


