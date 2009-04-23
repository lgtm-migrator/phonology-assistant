﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.1433
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SIL.Pa.Updates.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "2.0.0.0")]
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SIL.Pa.Updates.Properties.Resources", typeof(Resources).Assembly);
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
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;FwQueries&gt;
        ///	&lt;!-- The following query is obsolete since DB names are now used as project names. --&gt;
        ///	&lt;projectname&gt;select TOP 1 txt from CmProject_Name&lt;/projectname&gt;
        ///	&lt;lastmodifiedstamp&gt;
        ///		select top 1 Updstmp from CmObject where class$ in (5002, 5016) order by Updstmp desc
        ///	&lt;/lastmodifiedstamp&gt;
        ///	&lt;veracularwritingsystems&gt;
        ///		SELECT * FROM
        ///		LanguageProject_VernacularWritingSystems lp
        ///		INNER JOIN LgWritingSystem_Name lg
        ///		ON lp.Dst = lg.Obj
        ///	&lt;/veracularwritin [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string FwSQLQueries {
            get {
                return ResourceManager.GetString("FwSQLQueries", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;FwQueries&gt;
        ///	&lt;!-- The following query is obsolete since DB names are now used as project names. --&gt;
        ///	&lt;projectname&gt;select TOP 1 txt from CmProject_Name&lt;/projectname&gt;
        ///	&lt;lastmodifiedstamp&gt;
        ///		select top 1 Updstmp from CmObject where class$ in (5002, 5016) order by Updstmp desc
        ///	&lt;/lastmodifiedstamp&gt;
        ///	&lt;veracularwritingsystems&gt;
        ///		SELECT * FROM
        ///		LangProject_VernWss lp
        ///		INNER JOIN LgWritingSystem_Name lg
        ///		ON lp.Dst = lg.Obj
        ///	&lt;/veracularwritingsystems&gt;
        ///	&lt;analysis [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string FwSQLQueriesShortNames {
            get {
                return ResourceManager.GetString("FwSQLQueriesShortNames", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to There was an error when trying to apply the update.\nYou may not have sufficient permissions.\n\n{0}.
        /// </summary>
        internal static string kstidErrorUpdating {
            get {
                return ResourceManager.GetString("kstidErrorUpdating", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The folder in which Phonology Assistant was installed could not be located. Please specify that folder..
        /// </summary>
        internal static string kstidInstallFolderMissing {
            get {
                return ResourceManager.GetString("kstidInstallFolderMissing", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Phonology Assistant Updater.
        /// </summary>
        internal static string kstidMsgBoxCaption {
            get {
                return ResourceManager.GetString("kstidMsgBoxCaption", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The folder you selected appears not to contain an installation\nof Phonology Assistant. If you continue, it is unlikely the update\nwill be applied properly.\n\nDo you want to specify this folder anyway?.
        /// </summary>
        internal static string kstidSuspectFolderMsg {
            get {
                return ResourceManager.GetString("kstidSuspectFolderMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Update has been applied..
        /// </summary>
        internal static string kstidUpdateCompleteMsg {
            get {
                return ResourceManager.GetString("kstidUpdateCompleteMsg", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This update only affects Phonology Assistant projects containing FieldWorks data sources. It is needed in order for Phonology Assistant to read FieldWorks data sources created or updated using FieldWorks 5.3 or later. In addition, this update corrects two known problems:\n\n1) Under certain circumstances, Phonolgy Assistant does not find all the phonetic data in a FieldWorks database or creates too many records when reading a FieldWorks database.\n\n2) Under certain circumstances, Phonology Assistant does n [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string kstidUpdateMsg {
            get {
                return ResourceManager.GetString("kstidUpdateMsg", resourceCulture);
            }
        }
    }
}