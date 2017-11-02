﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Markup;
using System.Reflection;

namespace DIPOL_UF.Commands
{
    /// <summary>
    /// XAML extension used to reroute events to ViewModel commands.
    /// </summary>
    class EventToCommndExtension : MarkupExtension
    {
        /// <summary>
        /// ViewModel command name.
        /// </summary>
        private string commandName = null;

        /// <summary>
        /// Invoked by XAML code when property value is needed.
        /// </summary>
        /// <param name="serviceProvider">Used to query state of the bound object and property. Assigned when called from XAML.</param>
        /// <returns>A delegate of type compatible with the bound event handler.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Retrieves information about bound target (FrameworkElement) and preoprty (event)
            var valueTarget = (serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget);

            // Type of the event handler
            Type delegateType;

            // Target property can be either MethodInfo or EventInfo
            // If MethodInfo, its signature is (object sender, *EventHandler handler), retrieve type from second argument
            if (valueTarget.TargetProperty is MethodInfo boundPropertyInfo)
                delegateType = boundPropertyInfo.GetParameters()[1].ParameterType;
            // If EventInfo, it's just EventHandlerType property value
            else if (valueTarget.TargetProperty is EventInfo boundEventInfo)
                delegateType = boundEventInfo.EventHandlerType;
            // Otherwise throws
            else
                throw new Exception();
            
            // Parameters of the *EventHandler retrieved from Invoke method.
            var delegateParameterTypes = delegateType
                .GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance)
                ?.GetParameters()
                .Select(p => p.ParameterType)
                .ToArray();

            // Constructs appropriate *EventHandler from generic DoAction<T>, substituting *EventArgs type for T
            var eventHandlerMethodInfo = this
                .GetType()
                .GetRuntimeMethods()
                .First(mi => mi.Name == "DoAction")
                .MakeGenericMethod(delegateParameterTypes[1]);

            // Essentially returns this.DoAction<*EventArgs> with *EventArgs matching bound event handler signature.
            return  Delegate.CreateDelegate(delegateType, this, eventHandlerMethodInfo);
         
        }

        /// <summary>
        /// Template that is used to construct event handlers. Propagates events towards command.
        /// </summary>
        /// <typeparam name="T">Type of event EventArgs.</typeparam>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event arguments.</param>
        private void DoAction<T>(object sender, T e) where T: RoutedEventArgs
        {
            // Just to make sure we are dealing with UI element
            if (sender is FrameworkElement element)
            {
                // Packages sender and e into container class sent to ICommand.Execute(object) parameter
                var commandArgs = new EventCommandArgs<T>(sender, e);

                // Retrieves ViewModel, then property to which event is bound, then value of this property, which should be ICommand.
                var delegateCommand = element
                    .DataContext
                    .GetType()
                    .GetProperty(commandName, BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(element.DataContext) as ICommand;

                // If can be executed, executes
                if (delegateCommand?.CanExecute(commandArgs) ?? false)
                    delegateCommand.Execute(commandArgs);
                
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="commandName">First parameter of the binding. Text name of the <see cref="ICommand"/> ViewModel property.</param>
        public EventToCommndExtension(object commandName)
        {
            // Right now supports only property name binding.
            if (commandName is string command)
                this.commandName = command;
            else throw new ArgumentException();
                        
        }
    }
}
