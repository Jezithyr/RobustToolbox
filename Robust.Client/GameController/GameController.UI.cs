using System;
using System.Collections.Generic;
using System.Runtime.Loader;
using Robust.Shared.UserInterface;

namespace Robust.Client
{
    internal sealed partial class GameController
    {
        private List<UICore> _uiEntryPoints = new();
        private AssemblyLoadContext _uiAssemblyContext = default!;
        internal AssemblyLoadContext UIAssemblyContext => _uiAssemblyContext;

        private AssemblyLoadContext CreateUIAssemblyContext()
        {
            _uiAssemblyContext = new AssemblyLoadContext("UIAssemblyContext", true);
            return _uiAssemblyContext;
        }

        private void LoadUICores()
        {
        }
    }
}
