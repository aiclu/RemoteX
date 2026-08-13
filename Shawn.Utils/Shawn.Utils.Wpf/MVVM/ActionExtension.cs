using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xaml;

namespace Shawn.Utils.Wpf.MVVM
{
    /// <summary>
    /// MarkupExtension used for binding Commands and Events to methods on the View.ActionTarget
    /// </summary>
    public class ActionExtension : MarkupExtension
    {
        /// <summary>
        /// Gets or sets the name of the method to call
        /// </summary>
        [ConstructorArgument("method")]
        public string Method { get; set; } = "";

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionExtension"/> class
        /// </summary>
        public ActionExtension()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionExtension"/> class with the given method name
        /// </summary>
        /// <param name="method">Name of the method to call</param>
        public ActionExtension(string method)
        {
            if (string.IsNullOrEmpty(method))
                throw new InvalidOperationException("Method has not been set");
            this.Method = method;
        }


        /// <summary>
        /// When implemented in a derived class, returns an object that is provided as the value of the target property for this markup extension.
        /// </summary>
        /// <param name="serviceProvider">A service provider helper that can provide services for the markup extension.</param>
        /// <returns>The object value to set on the property where the extension is applied.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(this.Method))
                throw new InvalidOperationException("Method has not been set");

            var valueService = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;

            switch (valueService?.TargetObject)
            {
                case DependencyObject targetObject:
                    return this.HandleDependencyObject(serviceProvider, valueService, targetObject);
                case CommandBinding commandBinding:
                    return this.CreateEventAction(serviceProvider, null, ((EventInfo)valueService.TargetProperty).EventHandlerType, isCommandBinding: true);
                default:
                    // Seems this is the case when we're in a template. We'll get called again properly in a second.
                    // http://social.msdn.microsoft.com/Forums/vstudio/en-US/a9ead3d5-a4e4-4f9c-b507-b7a7d530c6a9/gaining-access-to-target-object-instead-of-shareddp-in-custom-markupextensions-providevalue-method?forum=wpf
                    return this;
            }
        }

        private object HandleDependencyObject(IServiceProvider serviceProvider, IProvideValueTarget valueService, DependencyObject targetObject)
        {
            switch (valueService.TargetProperty)
            {
                case DependencyProperty dependencyProperty when dependencyProperty.PropertyType == typeof(ICommand):
                    // If they're in design mode and haven't set View.ActionTarget, default to looking sensible
                    return this.CreateCommandAction(serviceProvider, targetObject);
                case EventInfo eventInfo:
                    return this.CreateEventAction(serviceProvider, targetObject, eventInfo.EventHandlerType);
                case MethodInfo methodInfo: // For attached events
                    {
                        var parameters = methodInfo.GetParameters();
                        if (parameters.Length == 2 && typeof(Delegate).IsAssignableFrom(parameters[1].ParameterType))
                        {
                            return this.CreateEventAction(serviceProvider, targetObject, parameters[1].ParameterType);
                        }
                        throw new ArgumentException("Action used with an attached event (or something similar) which didn't follow the normal pattern");
                    }
                default:
                    throw new ArgumentException("Can only use ActionExtension with a Command property or an event handler");
            }
        }

        private ICommand CreateCommandAction(IServiceProvider serviceProvider, DependencyObject targetObject)
        {
            var rootObjectProvider = serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
            var rootObject = rootObjectProvider?.RootObject as DependencyObject;
            return new CommandAction(targetObject, rootObject, this.Method);
        }

        private Delegate CreateEventAction(IServiceProvider serviceProvider, DependencyObject? targetObject, Type? eventType, bool isCommandBinding = false)
        {
            EventAction ec;
            var rootObjectProvider = serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
            var rootObject = rootObjectProvider?.RootObject as DependencyObject;
            if (isCommandBinding)
            {
                if (rootObject == null)
                    throw new InvalidOperationException("Action may only be used with CommandBinding from a XAML view (unable to retrieve IRootObjectProvider.RootObject)");
                ec = new EventAction(rootObject, null, eventType, this.Method);
            }
            else
            {
                ec = new EventAction(targetObject, rootObject, eventType, this.Method);
            }
            return ec.GetDelegate();
        }
    }

    /// <summary>
    /// The View.ActionTarget was not set. This probably means the item is in a ContextMenu/Popup
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class ActionNotSetException : Exception
    {
        internal ActionNotSetException(string message) : base(message) { }
    }

    /// <summary>
    /// The Action Target was null, and shouldn't have been (NullTarget = Throw)
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class ActionTargetNullException : Exception
    {
        internal ActionTargetNullException(string message) : base(message) { }
    }

    /// <summary>
    /// The method specified could not be found on the Action Target
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class ActionNotFoundException : Exception
    {
        internal ActionNotFoundException(string message) : base(message) { }
    }

    /// <summary>
    /// The method specified does not have the correct signature
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable")]
    public class ActionSignatureInvalidException : Exception
    {
        internal ActionSignatureInvalidException(string message) : base(message) { }
    }
}
