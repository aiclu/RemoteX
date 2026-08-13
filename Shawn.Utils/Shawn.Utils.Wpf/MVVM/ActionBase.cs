using System;
using System.Data;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows;

namespace Shawn.Utils.Wpf.MVVM
{
    /// <summary>
    /// Common base class for CommandAction and EventAction
    /// </summary>
    public abstract class ActionBase : DependencyObject
    {
        /// <summary>
        /// Gets the View to grab the View.ActionTarget from
        /// </summary>
        public DependencyObject? Subject { get; private set; }

        /// <summary>
        /// Gets the method name. E.g. if someone's gone Button Command="{s:Action MyMethod}", this is MyMethod.
        /// </summary>
        public string MethodName { get; private set; }

        /// <summary>
        /// Gets the MethodInfo for the method to call. This has to exist, or we throw a wobbly
        /// </summary>
        protected MethodInfo? TargetMethodInfo { get; private set; }


        protected DependencyObject? TargetSubject = null;

        private void SetTargetMethodInfo()
        {
            if (this.Target is FrameworkElement element && element.DataContext != null)
            {
                BindingFlags bindingFlags;
                if (element.DataContext is Type newTargetType)
                {
                    bindingFlags = BindingFlags.Public | BindingFlags.Static;
                }
                else
                {
                    newTargetType = element.DataContext.GetType();
                    bindingFlags = BindingFlags.Public | BindingFlags.Instance;
                }

                try
                {
                    var targetMethodInfo = newTargetType.GetMethod(this.MethodName, bindingFlags);
                    if (targetMethodInfo != null)
                    {
                        this.AssertTargetMethodInfo(targetMethodInfo, newTargetType);
                        this.TargetMethodInfo = targetMethodInfo;
                        return;
                    }
                }
                catch (AmbiguousMatchException e)
                {
                    var ex = new AmbiguousMatchException($"Ambiguous match for {this.MethodName} method on {newTargetType.Name}", e);
                    throw ex;
                }
            }


            {
                BindingFlags bindingFlags;
                if (this.Target is Type newTargetType)
                {
                    bindingFlags = BindingFlags.Public | BindingFlags.Static;
                }
                else
                {
                    newTargetType = this.Target.GetType();
                    bindingFlags = BindingFlags.Public | BindingFlags.Instance;
                }

                try
                {
                    var targetMethodInfo = newTargetType.GetMethod(this.MethodName, bindingFlags);
                    if (targetMethodInfo != null)
                    {
                        this.AssertTargetMethodInfo(targetMethodInfo, newTargetType);
                        this.TargetMethodInfo = targetMethodInfo;
                        return;
                    }
                }
                catch (AmbiguousMatchException e)
                {
                    var ex = new AmbiguousMatchException($"Ambiguous match for {this.MethodName} method on {newTargetType.Name}", e);
                    throw ex;
                }
            }


            throw new ActionNotFoundException($"Unable to find method {this.MethodName} on {TargetName()}");
        }



        private object _target = new object();
        /// <summary>
        /// Gets the object on which methods will be invoked
        /// </summary>
        public object Target
        {
            get => _target;
            set
            {
                if (_target == value) return;
                this.OnTargetChanged(_target, value);

                if (value is FrameworkElement element && element.DataContext != null)
                {
                    BindingFlags bindingFlags;
                    if (element.DataContext is Type newTargetType)
                    {
                        bindingFlags = BindingFlags.Public | BindingFlags.Static;
                    }
                    else
                    {
                        newTargetType = element.DataContext.GetType();
                        bindingFlags = BindingFlags.Public | BindingFlags.Instance;
                    }

                    try
                    {
                        var targetMethodInfo = newTargetType.GetMethod(this.MethodName, bindingFlags);
                        if (targetMethodInfo != null)
                        {
                            this.AssertTargetMethodInfo(targetMethodInfo, newTargetType);
                            this.TargetMethodInfo = targetMethodInfo;
                            _target = element.DataContext;
                            return;
                        }
                    }
                    catch (AmbiguousMatchException e)
                    {
                        var ex = new AmbiguousMatchException($"Ambiguous match for {this.MethodName} method on {newTargetType.Name}", e);
                        throw ex;
                    }
                }


                {
                    BindingFlags bindingFlags;
                    if (value is Type newTargetType)
                    {
                        bindingFlags = BindingFlags.Public | BindingFlags.Static;
                    }
                    else
                    {
                        newTargetType = this.Target.GetType();
                        bindingFlags = BindingFlags.Public | BindingFlags.Instance;
                    }

                    try
                    {
                        var targetMethodInfo = newTargetType.GetMethod(this.MethodName, bindingFlags);
                        if (targetMethodInfo != null)
                        {
                            this.AssertTargetMethodInfo(targetMethodInfo, newTargetType);
                            this.TargetMethodInfo = targetMethodInfo;
                            _target = value;
                            return;
                        }
                    }
                    catch (AmbiguousMatchException e)
                    {
                        var ex = new AmbiguousMatchException($"Ambiguous match for {this.MethodName} method on {newTargetType.Name}", e);
                        throw ex;
                    }
                }

                throw new ActionNotFoundException($"Unable to find method {this.MethodName} on {value.GetType().Name}");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionBase"/> class to use <see cref="View.ActionTargetProperty"/> to get the target
        /// </summary>
        /// <param name="subject">View to grab the View.ActionTarget from</param>
        /// <param name="targetSubject">Backup subject to use if no ActionTarget could be retrieved from the subject</param>
        /// <param name="methodName">Method name. the MyMethod in Button Command="{s:Action MyMethod}".</param>
        protected ActionBase(DependencyObject? subject, DependencyObject? targetSubject, string methodName) : this(methodName)
        {
            this.Subject = subject;
            this.Target = targetSubject ?? throw new NoNullAllowedException();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionBase"/> class to use an explicit target
        /// </summary>
        /// <param name="target">Target to find the method on</param>
        /// <param name="methodName">Method name. the MyMethod in Button Command="{s:Action MyMethod}".</param>
        protected ActionBase(object? target, string methodName) : this(methodName)
        {
            this.Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        private ActionBase(string methodName)
        {
            this.MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        }
        

        /// <summary>
        /// Invoked when a new non-null target is set, which has non-null MethodInfo. Used to assert that the method signature is correct
        /// </summary>
        /// <param name="targetMethodInfo">MethodInfo of method on new target</param>
        /// <param name="newTargetType">Type of new target</param>
        private protected abstract void AssertTargetMethodInfo(MethodInfo targetMethodInfo, Type newTargetType);

        /// <summary>
        /// Invoked when a new target is set, after all other action has been taken
        /// </summary>
        /// <param name="oldTarget">Previous target</param>
        /// <param name="newTarget">New target</param>
        private protected virtual void OnTargetChanged(object oldTarget, object newTarget) { }

        /// <summary>
        /// Assert that the target is not View.InitialActionTarget
        /// </summary>
        private protected void AssertTargetSet()
        {
            if (this.TargetMethodInfo == null)
            {
                var ex = new ActionNotFoundException($"Unable to find method {this.MethodName} on {this.TargetName()}");
                throw ex;
            }
        }

        /// <summary>
        /// Invoke the target method with the given parameters
        /// </summary>
        /// <param name="parameters">Parameters to pass to the target method</param>
        private protected void InvokeTargetMethod(object?[]? parameters)
        {
            //this.logger.Info("Invoking method {0} on {1} with parameters ({2})", this.MethodName, this.TargetName(), parameters == null ? "none" : String.Join(", ", parameters));
            if (this.TargetMethodInfo == null)
            {
                var ex = new ActionNotFoundException($"Unable to find method {this.MethodName} on {this.TargetName()}");
                throw ex;
            }

            try
            {
                var target = this.TargetMethodInfo.IsStatic ? null : this.Target;
                var result = this.TargetMethodInfo.Invoke(target, parameters);
                // Be nice and make sure that any exceptions get rethrown
                if (result is Task task)
                {
                    AwaitTask(task);
                }
            }
            catch (TargetInvocationException e)
            {
                // Be nice and unwrap this for them
                // They want a stack track for their VM method, not us
                //this.logger.Error(e.InnerException, String.Format("Failed to invoke method {0} on {1} with parameters ({2})", this.MethodName, this.TargetName(), parameters == null ? "none" : String.Join(", ", parameters)));
                // http://stackoverflow.com/a/17091351/1086121
                if (e.InnerException != null)
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                else
                    throw;
            }

            async void AwaitTask(Task t) => await t;
        }

        private string TargetName()
        {
            return this.Target is Type t
                ? $"static target {t.Name}"
                : $"target {this.Target?.GetType().Name}";
        }
    }
}
