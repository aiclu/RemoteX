using System;
using System.Data;
using System.Reflection;
using System.Windows;
using Shawn.Utils.Wpf.MVVM;

namespace Shawn.Utils.Wpf.MVVM
{
    /// <summary>
    /// Created by ActionExtension, this can return a delegate suitable adding binding to an event, and can call a method on the View.ActionTarget
    /// </summary>
    public class EventAction : ActionBase
    {
        private static readonly MethodInfo?[] InvokeCommandMethodInfos = new[]
        {
            typeof(EventAction).GetMethod("InvokeEventArgsCommand", BindingFlags.NonPublic | BindingFlags.Instance),
            typeof(EventAction).GetMethod("InvokeDependencyCommand", BindingFlags.NonPublic | BindingFlags.Instance),
        };

        /// <summary>
        /// Type of event handler
        /// </summary>
        private readonly Type _eventHandlerType;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventAction"/> classto use <see cref="View.ActionTargetProperty"/> to get the target
        /// </summary>
        /// <param name="subject">View whose View.ActionTarget we watch</param>
        /// <param name="targetSubject">Backup subject to use if no ActionTarget could be retrieved from the subject</param>
        /// <param name="eventHandlerType">Type of event handler we're returning a delegate for</param>
        /// <param name="methodName">The MyMethod in {s:Action MyMethod}, this is what we call when the event's fired</param>
        public EventAction(DependencyObject? subject, DependencyObject? targetSubject, Type? eventHandlerType, string methodName)
            : base(subject, targetSubject, methodName)
        {
            this._eventHandlerType = eventHandlerType ?? throw new NoNullAllowedException();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventAction"/> class to use an explicit target
        /// </summary>
        /// <param name="target">Target to find the method on</param>
        /// <param name="eventHandlerType">Type of event handler we're returning a delegate for</param>
        /// <param name="methodName">The MyMethod in {s:Action MyMethod}, this is what we call when the event's fired</param>
        public EventAction(object target, Type? eventHandlerType, string methodName) : base(target, methodName)
        {
            this._eventHandlerType = eventHandlerType ?? throw new NoNullAllowedException();
        }

        /// <summary>
        /// Invoked when a new non-null target is set, which has non-null MethodInfo. Used to assert that the method signature is correct
        /// </summary>
        /// <param name="targetMethodInfo">MethodInfo of method on new target</param>
        /// <param name="newTargetType">Type of new target</param>
        private protected override void AssertTargetMethodInfo(MethodInfo targetMethodInfo, Type newTargetType)
        {
            var methodParameters = targetMethodInfo.GetParameters();
            if (!(methodParameters.Length == 0 ||
                (methodParameters.Length == 1 && (typeof(EventArgs).IsAssignableFrom(methodParameters[0].ParameterType) || methodParameters[0].ParameterType == typeof(DependencyPropertyChangedEventArgs))) ||
                (methodParameters.Length == 2 && (typeof(EventArgs).IsAssignableFrom(methodParameters[1].ParameterType) || methodParameters[1].ParameterType == typeof(DependencyPropertyChangedEventArgs)))))
            {
                var e = new ActionSignatureInvalidException($"Method {this.MethodName} on {newTargetType.Name} must have the signatures Method(), Method(EventArgsOrSubClass e), or Method(object sender, EventArgsOrSubClass e)");
                throw e;
            }
        }

        /// <summary>
        /// Return a delegate which can be added to the targetProperty
        /// </summary>
        /// <returns>An event handler, which, when invoked, will invoke the action</returns>
        public Delegate GetDelegate()
        {
            Delegate? del = null;
            foreach (var invokeCommandMethodInfo in InvokeCommandMethodInfos)
            {
                if (invokeCommandMethodInfo != null)
                    del = Delegate.CreateDelegate(this._eventHandlerType, this, invokeCommandMethodInfo, false);
                if (del != null)
                    break;
            }

            if (del == null)
            {
                var msg = $@"Event being bound to does not have a signature we know about. Method {this.MethodName} on target {this.Target}. "
                          + "Valid signatures are:"
                          + "Valid signatures are:\n"
                          + " - '(object sender, EventArgsOrSubclass e)'\n"
                          + " - '(object sender, DependencyPropertyChangedEventArgs e)'";
                var e = new ActionEventSignatureInvalidException(msg);
                throw e;
            }

            return del;
        }

        private void InvokeDependencyCommand(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.InvokeCommand(sender, e);
        }

        private void InvokeEventArgsCommand(object sender, EventArgs e)
        {
            this.InvokeCommand(sender, e);
        }

        // ReSharper disable once UnusedMember.Local
        private void InvokeCommand(object sender, object e)
        {
            this.AssertTargetSet();

            // Any throwing will have been handled above
            if (this.Target == null || this.TargetMethodInfo == null)
                return;

            object[]? parameters;
            switch (this.TargetMethodInfo.GetParameters().Length)
            {
                case 1:
                    parameters = new object[] { e };
                    break;

                case 2:
                    parameters = new[] { sender, e };
                    break;

                default:
                    parameters = null;
                    break;
            }
            this.InvokeTargetMethod(parameters);
        }
    }

    /// <summary>
    /// You tried to use an EventAction with an event that doesn't follow the EventHandler signature
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class ActionEventSignatureInvalidException : Exception
    {
        internal ActionEventSignatureInvalidException(string message) : base(message) { }
    }
}
