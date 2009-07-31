// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <author name="Daniel Grunwald"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text;
using CommandManager=ICSharpCode.Core.Presentation.CommandManager;

namespace ICSharpCode.Core.Presentation
{
	class CommandWrapper : System.Windows.Input.ICommand
	{
		public static System.Windows.Input.ICommand GetCommand(Codon codon, object caller, bool createCommand)
		{
			string commandName = codon.Properties["command"];
			if (!string.IsNullOrEmpty(commandName)) {
				var wpfCommand = MenuService.GetRegisteredCommand(codon.AddIn, commandName);
				if (wpfCommand != null) {
					return wpfCommand;
				} else {
					MessageService.ShowError("Could not find WPF command '" + commandName + "'.");
					// return dummy command
					return new CommandWrapper(codon, caller, null);
				}
			}
			return new CommandWrapper(codon, caller, createCommand);
		}
		
		bool commandCreated;
		ICommand addInCommand;
		readonly Codon codon;
		readonly object caller;
		
		public CommandWrapper(Codon codon, object caller, bool createCommand)
		{
			this.codon = codon;
			this.caller = caller;
			if (createCommand) {
				commandCreated = true;
				CreateCommand();
			}
		}
		
		public CommandWrapper(Codon codon, object caller, ICommand command)
		{
			this.codon = codon;
			this.caller = caller;
			this.addInCommand = command;
			commandCreated = true;
		}
		
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification="We're displaying the message to the user.")]
		void CreateCommand()
		{
			commandCreated = true;
			try {
				string link = codon.Properties["link"];
				ICommand menuCommand;
				if (link != null && link.Length > 0) {
					if (MenuService.LinkCommandCreator == null)
						throw new NotSupportedException("MenuCommand.LinkCommandCreator is not set, cannot create LinkCommands.");
					menuCommand = MenuService.LinkCommandCreator(codon.Properties["link"]);
				} else {
					menuCommand = (ICommand)codon.AddIn.CreateObject(codon.Properties["class"]);
				}
				if (menuCommand != null) {
					menuCommand.Owner = caller;
				}
				addInCommand = menuCommand;
			} catch (Exception e) {
				MessageService.ShowError(e, "Can't create menu command : " + codon.Id);
			}
		}
		
		public event EventHandler CanExecuteChanged {
			add { System.Windows.Input.CommandManager.RequerySuggested += value; }
			remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
		}
		
		public void Execute(object parameter)
		{
			if (!commandCreated) {
				CreateCommand();
			}
			LoggingService.Debug("Execute " + codon.Id);
			if (CanExecute(parameter)) {
				addInCommand.Run();
			}
		}
		
		public bool CanExecute(object parameter)
		{
			//LoggingService.Debug("CanExecute " + codon.Id);
			if (codon.GetFailedAction(caller) != ConditionFailedAction.Nothing)
				return false;
			if (!commandCreated)
				return true;
			if (addInCommand == null)
				return false;
			IMenuCommand menuCommand = addInCommand as IMenuCommand;
			if (menuCommand != null) {
				return menuCommand.IsEnabled;
			} else {
				return true;
			}
		}
	}
	
	class MenuCommand : CoreMenuItem
	{
		public MenuCommand(UIElement inputBindingOwner, Codon codon, object caller, bool createCommand) : base(codon, caller)
		{
			string routedCommandName = null;
			if(codon.Properties.Contains("command")) {
				routedCommandName = codon.Properties["command"];				
			} else if(codon.Properties.Contains("link") || codon.Properties.Contains("class")) {
				routedCommandName = string.IsNullOrEmpty(codon.Properties["link"]) ? codon.Properties["class"] : codon.Properties["link"];
			}

			var routedCommand = CommandManager.GetRoutedUICommand(routedCommandName);
			if(routedCommand != null) {
				this.Command = routedCommand;
			}
			
			// Register input bindings update handler
			BindingsUpdatedHandler gesturesUpdateHandler = delegate {
				var gesturesTemplate = new BindingInfoTemplate();
				gesturesTemplate.RoutedCommandName = routedCommandName;
				
				if(codon.Properties.Contains("ownerinstance")) {
					gesturesTemplate.OwnerInstanceName = codon.Properties["ownerinstance"];
				} else if(codon.Properties.Contains("ownertype")) {
					gesturesTemplate.OwnerTypeName = codon.Properties["ownertype"];
				}
				
				var updatedGestures = CommandManager.FindInputGestures(BindingInfoMatchType.SuperSet, gesturesTemplate);
				
				this.InputGestureText = (string)new InputGestureCollectionConverter().ConvertToInvariantString(updatedGestures);
			};
			gesturesUpdateHandler.Invoke(this, new BindingsUpdatedHandlerArgs());
			
			var bindingTemplate = new BindingInfoTemplate();
			if(codon.Properties.Contains("ownerinstance")) {
				bindingTemplate.OwnerInstanceName = codon.Properties["ownerinstance"];
			} else if(codon.Properties.Contains("ownertype")) {
				bindingTemplate.OwnerTypeName = codon.Properties["ownertype"];
			}
			bindingTemplate.RoutedCommandName = routedCommandName;
			
			CommandManager.RegisterInputBindingsUpdateHandler(bindingTemplate, gesturesUpdateHandler);
		}
	}
}
