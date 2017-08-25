using System;
using System.Windows.Input;

namespace LSLBridge.Helpers
{
    public class CommandHandler : ICommand
    {
        public event EventHandler CanExecuteChanged;
        private Action<object> actionWithParam;
        private Action actionNoParam;
        private bool canExecute;

        public CommandHandler(Action<object> action, bool canExecute)
        {
            CanExecuteChanged += CommandHandler_CanExecuteChanged;
            actionWithParam = action;
            this.canExecute = canExecute;
        }

        private void CommandHandler_CanExecuteChanged(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        public CommandHandler(Action action, bool canExecute)
        {
            actionNoParam = action;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return canExecute;
        }

        public void Execute(object parameter = null)
        {
            if (parameter == null) actionNoParam();
            else actionWithParam(parameter);
        }
    }
}
