﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PRI.Messaging.Patterns.Analyzer {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("PRI.Messaging.Patterns.Analyzer.Resources", typeof(Resources).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Calls to RequestAsync should be awaited.
        /// </summary>
        internal static string Mp0100Description {
            get {
                return ResourceManager.GetString("Mp0100Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Call to RequestAsync not awaited..
        /// </summary>
        internal static string Mp0100MessageFormat {
            get {
                return ResourceManager.GetString("Mp0100MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Call to RequestAsync not awaited..
        /// </summary>
        internal static string Mp0100Title {
            get {
                return ResourceManager.GetString("Mp0100Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Calls to RequestAsync with error events should be guarded with try/catch.
        /// </summary>
        internal static string Mp0101Description {
            get {
                return ResourceManager.GetString("Mp0101Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Call to RequestAsync with error event but no try/catch..
        /// </summary>
        internal static string Mp0101MessageFormat {
            get {
                return ResourceManager.GetString("Mp0101MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Call to RequestAsync with error event but no try.catch..
        /// </summary>
        internal static string Mp0101Title {
            get {
                return ResourceManager.GetString("Mp0101Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Type names should be all uppercase..
        /// </summary>
        internal static string Mp0102Description {
            get {
                return ResourceManager.GetString("Mp0102Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Call to RequestAsync can be replaced with IConsumer&lt;T&gt; implementations..
        /// </summary>
        internal static string Mp0102MessageFormat {
            get {
                return ResourceManager.GetString("Mp0102MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Consider a looser-coupled, message handler class..
        /// </summary>
        internal static string Mp0102Title {
            get {
                return ResourceManager.GetString("Mp0102Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to RequestAsync models message-event interaction, not event-event interaction.
        /// </summary>
        internal static string Mp0103Description {
            get {
                return ResourceManager.GetString("Mp0103Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Sending event with RequestAsync..
        /// </summary>
        internal static string Mp0103MessageFormat {
            get {
                return ResourceManager.GetString("Mp0103MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to RequestAsync called with event as message to send..
        /// </summary>
        internal static string Mp0103Title {
            get {
                return ResourceManager.GetString("Mp0103Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message class has no handler.
        /// </summary>
        internal static string Mp0104Description {
            get {
                return ResourceManager.GetString("Mp0104Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message class has no handler.
        /// </summary>
        internal static string Mp0104MessageFormat {
            get {
                return ResourceManager.GetString("Mp0104MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message class has no handler.
        /// </summary>
        internal static string Mp0104Title {
            get {
                return ResourceManager.GetString("Mp0104Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message type identifiers should not have incorrect suffix: consider &quot;Message&quot;, &quot;Command&quot;, or &quot;Request&quot; for IMessage-derived type identifiers, or &quot;Event&quot;, or &quot;Response&quot; for IEvent type identifiers..
        /// </summary>
        internal static string Mp0110Description {
            get {
                return ResourceManager.GetString("Mp0110Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message type identifier &quot;{0}&quot; has incorrect suffix..
        /// </summary>
        internal static string Mp0110MessageFormat {
            get {
                return ResourceManager.GetString("Mp0110MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message type identifiier has incorrect suffix..
        /// </summary>
        internal static string Mp0110Title {
            get {
                return ResourceManager.GetString("Mp0110Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IConsumer&lt;T&gt; implementation identifiers should not have incorrect suffix: consider &quot;Handler&quot; for IConsumer&lt;T&gt; type identifiers..
        /// </summary>
        internal static string Mp0111Description {
            get {
                return ResourceManager.GetString("Mp0111Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IConsumer&lt;T&gt; type identifier &quot;{0}&quot; has incorrect suffix..
        /// </summary>
        internal static string Mp0111MessageFormat {
            get {
                return ResourceManager.GetString("Mp0111MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IConsumer&lt;T&gt; type identifier has incorrect suffix..
        /// </summary>
        internal static string Mp0111Title {
            get {
                return ResourceManager.GetString("Mp0111Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IBus.Send exists to be explicit about sending messages and IBus.Publish to be explicit about publishing events..
        /// </summary>
        internal static string Mp0112Description {
            get {
                return ResourceManager.GetString("Mp0112Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IBus.Send called with and IEvent argument &quot;{0}&quot; instead of IBus.Publish..
        /// </summary>
        internal static string Mp0112MessageFormat {
            get {
                return ResourceManager.GetString("Mp0112MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IBus.Send has incorrect argument type..
        /// </summary>
        internal static string Mp0112Title {
            get {
                return ResourceManager.GetString("Mp0112Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IConsumer&lt;T&gt; implementation type names should have form &quot;&lt;MessageType&gt;Handler&quot;..
        /// </summary>
        internal static string Mp0113Description {
            get {
                return ResourceManager.GetString("Mp0113Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &quot;{0}&quot; is poorly named, consider naming it &quot;{1}&quot;..
        /// </summary>
        internal static string Mp0113MessageFormat {
            get {
                return ResourceManager.GetString("Mp0113MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IConsumer&lt;T&gt; implementation type has incorrect name..
        /// </summary>
        internal static string Mp0113Title {
            get {
                return ResourceManager.GetString("Mp0113Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message types should be either consumed or produced..
        /// </summary>
        internal static string Mp0114Description {
            get {
                return ResourceManager.GetString("Mp0114Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} is neither an argument of IConsumer&lt;T&gt; nor a argument to IBus.Send or IBus.Publish..
        /// </summary>
        internal static string Mp0114MessageFormat {
            get {
                return ResourceManager.GetString("Mp0114MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message types should be either consumed or produced..
        /// </summary>
        internal static string Mp0114Title {
            get {
                return ResourceManager.GetString("Mp0114Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Command handlers should publish events describing state changes..
        /// </summary>
        internal static string Mp0115Description {
            get {
                return ResourceManager.GetString("Mp0115Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} uses the Command Message convention to signify a state change request but does not publish events describing state changes..
        /// </summary>
        internal static string Mp0115MessageFormat {
            get {
                return ResourceManager.GetString("Mp0115MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Command handlers should publish events describing state changes..
        /// </summary>
        internal static string Mp0115Title {
            get {
                return ResourceManager.GetString("Mp0115Title", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message handlers should propagate correlation id when producing messages..
        /// </summary>
        internal static string Mp0116Description {
            get {
                return ResourceManager.GetString("Mp0116Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} message handler {1} message but does not propagate correlation id..
        /// </summary>
        internal static string Mp0116MessageFormat {
            get {
                return ResourceManager.GetString("Mp0116MessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Message handlers should propagate correlation id when producing messages..
        /// </summary>
        internal static string Mp0116Title {
            get {
                return ResourceManager.GetString("Mp0116Title", resourceCulture);
            }
        }
    }
}
