using System;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;

namespace ExpectoRunner {
    public class InsertDateHandler : CommandHandler {
        protected override void Run() {
            Console.WriteLine(">>> Run called");
        }

        protected override void Update(CommandInfo info) {
            Console.WriteLine(">>> Update called");
        }
    }
}