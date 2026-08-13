using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Shawn.Utils.Wpf.MVVM;
using Expressions = System.Linq.Expressions;

namespace Shawn.Utils.Wpf.MVVM
{
    /// <summary>
    /// ICommand returned by ActionExtension for binding buttons, etc, to methods on a ViewModel.
    /// If the method has a parameter, CommandParameter is passed
    /// </summary>
    /// <remarks>
    /// Watches the current View.ActionTarget, and looks for a method with the given name, calling it when the ICommand is called.
    /// If a bool property with name Get(methodName) exists, it will be observed and used to enable/disable the ICommand.
    /// </remarks>
    public class CommandAction : ActionBase, ICommand
    {
        /// <summary>
        /// Generated accessor to grab the value of the guard property, or null if there is none
        /// </summary>
        private Func<bool>? _guardPropertyGetter = null;


        /// <summary>
        /// Initializes a new instance of the <see cref="CommandAction"/> class to use <see cref="View.ActionTargetProperty"/> to get the target
        /// </summary>
        /// <param name="subject">View to grab the View.ActionTarget from</param>
        /// <param name="targetSubject">Backup subject to use if no ActionTarget could be retrieved from the subject</param>
        /// <param name="methodName">Method name. the MyMethod in Button Command="{s:Action MyMethod}".</param>
        public CommandAction(DependencyObject subject, DependencyObject? targetSubject, string methodName) : base(subject, targetSubject, methodName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandAction"/> class to use an explicit target
        /// </summary>
        /// <param name="target">Target to find the method on</param>
        /// <param name="methodName">Method name. the MyMethod in Button Command="{s:Action MyMethod}".</param>
        public CommandAction(object target, string methodName) : base(target, methodName)
        { }

        private string GuardName => "Can" + this.MethodName;

        /// <summary>
        /// Invoked when a new non-null target is set, which has non-null MethodInfo. Used to assert that the method signature is correct
        /// </summary>
        /// <param name="targetMethodInfo">MethodInfo of method on new target</param>
        /// <param name="newTargetType">Type of new target</param>
        private protected override void AssertTargetMethodInfo(MethodInfo targetMethodInfo, Type newTargetType)
        {
            var methodParameters = targetMethodInfo.GetParameters();
            if (methodParameters.Length > 1)
            {
                var e = new ActionSignatureInvalidException(String.Format("Method {0} on {1} must have zero or one parameters", this.MethodName, newTargetType.Name));
                throw e;
            }
        }

        /// <summary>
        /// Invoked when a new target is set, after all other action has been taken
        /// </summary>
        /// <param name="oldTarget">Previous target</param>
        /// <param name="newTarget">New target</param>
        private protected override void OnTargetChanged(object oldTarget, object newTarget)
        {
            if (oldTarget is INotifyPropertyChanged oldInpc)
                PropertyChangedEventManager.RemoveHandler(oldInpc, this.PropertyChangedHandler, this.GuardName);

            this._guardPropertyGetter = null;
            var guardPropertyInfo = newTarget?.GetType().GetProperty(this.GuardName);
            if (guardPropertyInfo != null)
            {
                if (guardPropertyInfo.PropertyType == typeof(bool))
                {
                    var targetExpression = Expressions.Expression.Constant(newTarget);
                    var propertyAccess = Expressions.Expression.Property(targetExpression, guardPropertyInfo);
                    this._guardPropertyGetter = Expressions.Expression.Lambda<Func<bool>>(propertyAccess).Compile();
                }
                else
                {
                    //logger.Warn("Found guard property {0} for action {1} on target {2}, but its return type wasn't bool. Therefore, ignoring", this.GuardName, this.MethodName, newTarget);
                }
            }

            if (this._guardPropertyGetter != null)
            {
                if (newTarget is INotifyPropertyChanged inpc)
                    PropertyChangedEventManager.AddHandler(inpc, this.PropertyChangedHandler, this.GuardName);
            }

            this.UpdateCanExecute();
        }

        private void PropertyChangedHandler(object? sender, PropertyChangedEventArgs e)
        {
            this.UpdateCanExecute();
        }

        private void UpdateCanExecute()
        {
            var handler = this.CanExecuteChanged;
            // So. While we're safe firing PropertyChanged events on a non-UI thread, we
            // are not safe firing CanExecuteChanged events on other threads...
            // Therefore make sure we're on the UI thread
            if (handler != null)
            {
                //App.Current.Dispatcher.Invoke(() =>
                //{
                //    handler(this, EventArgs.Empty);
                //});
                Executor.OnUIThread(()=> handler(this, EventArgs.Empty));
            }
        }

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>true if this command can be executed; otherwise, false.</returns>
        public bool CanExecute(object? parameter)
        {
            // It's enabled only if both the targetNull and actionNonExistent tests pass
            if (this._guardPropertyGetter == null)
                return true;

            return this._guardPropertyGetter();
        }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler? CanExecuteChanged;

        /// <summary>
        /// The method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command. If the command does not require data to be passed, this object can be set to null.</param>
        public void Execute(object? parameter)
        {
            this.AssertTargetSet();

            // Any throwing would have been handled prior to this
            if (this.Target == null || this.TargetMethodInfo == null)
                return;

            // This is not going to be called very often, so don't bother to generate a delegate, in the way that we do for the method guard
            var parameters = this.TargetMethodInfo.GetParameters().Length == 1 ? new[] { parameter } : null;
            this.InvokeTargetMethod(parameters);
        }
    }
}
