// --------------------------------------------------------------------------------------------
#region // Copyright (c) 2003, SIL International. All Rights Reserved.   
// <copyright from='2003' to='2003' company='SIL International'>
//		Copyright (c) 2003, SIL International. All Rights Reserved.   
//    
//		Distributable under the terms of either the Common Public License or the
//		GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright> 
#endregion
// 
// File: StringTable.cs
// History: John Hatton
// Last reviewed: 
// 
// <remarks>
// </remarks>
// --------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

namespace SIL.Utils
{
	/// <summary>
	/// Get strings according to the current culture from one or more XML files
	/// </summary>
	public class StringTable
	{
		protected string m_baseDirectory;
		protected StringTable m_parent;
		protected XmlDocument m_document;
		private string m_sWsLoaded = null;

		/// <summary>
		/// This table is keyed by the groupXPathFragment passed to GetStringWithXPath.
		/// The value is another Dictioanry, from the id string to the string value we want.
		/// </summary>
		Dictionary<string, Dictionary<string, string>> m_pathsToStrings = new Dictionary<string, Dictionary<string, string>>();

		public StringTable(string baseDirectory)
		{
			m_parent = null;
			m_baseDirectory = baseDirectory;
			string sWs = CultureInfo.CurrentUICulture.Name.Replace('-', '_');
			//Load(sWs);
		}

		///// <summary>
		///// Load the strings for the given language/writing system/locale.
		///// </summary>
		///// <param name="sWs"></param>
		//private void Load(string sWs)
		//{
		//    string path = String.Empty;
		//    try
		//    {
		//        // Always load the neutral English strings first so that every string has a
		//        // fallback definition.
		//        if (m_sWsLoaded != "en")
		//        {
		//            path = ChooseStringFile(m_baseDirectory, "en");
		//            if (path == null)
		//                throw new FileNotFoundException("strings-en.xml does not exist in " + m_baseDirectory);
		//            m_document = new XmlDocument();
		//            m_document.Load(path);
		//        }
		//        if (sWs != m_sWsLoaded)
		//        {
		//            path = ChooseStringFile(m_baseDirectory, sWs);
		//            if (path != null)
		//            {
		//                XmlDocument doc2 = new XmlDocument();
		//                doc2.Load(path);
		//                MergeCustomTable(doc2);
		//            }
		//        }
		//    }
		//    catch (FileNotFoundException ex)
		//    {
		//        throw ex;
		//    }
		//    catch (Exception error)
		//    {
		//        m_sWsLoaded = "";
		//        throw new ApplicationException("Problem loading the strings table file in " + m_baseDirectory, error);
		//    }
		//    FindParent(m_baseDirectory);
		//}





		//private string GetTxtAtributeValue(XmlNode node)
		//{
		//    return XmlUtils.GetOptionalAttributeValue(
		//        node,
		//        "txt",
		//        XmlUtils.GetManditoryAttributeValue(node, "id")); // 'id' is default, if no 'txt' attribute is present.
		//}

		///// <summary>
		///// choose the best string file we can find for the given writing system/language.
		///// </summary>
		///// <param name="baseDirectory"></param>
		///// <returns></returns>
		//protected string ChooseStringFile(string baseDirectory, string sWs)
		//{
		//    string path = Path.Combine(baseDirectory, String.Format("strings-{0}.xml", sWs));
		//    if (File.Exists(path))
		//    {
		//        m_sWsLoaded = sWs;
		//        return path;
		//    }
		//    else
		//    {
		//        return null;
		//    }
		//}

		//protected void FindParent(string baseDirectory)
		//{
		//    XmlNode node = m_document.SelectSingleNode("strings");
		//    if (node == null)
		//        throw new ApplicationException("Could not find the root node, <strings> in " + baseDirectory);

			
		//    string inheritPath = XmlUtils.GetOptionalAttributeValue(node, "inheritPath");
		//    if (inheritPath != null && inheritPath.Length>0)
		//    {
		//        string path = Path.Combine(baseDirectory, inheritPath);
		//        m_parent = new StringTable(path);
		//    }
		//}

		///// <summary>
		///// get a string out of the root node of the string table
		///// </summary>
		///// <param name="id"></param>
		///// <returns></returns>
		//public string GetString(string id)
		//{
		//    return GetString(id, "");
		//}

		///// <summary>
		///// This is similar to GetStringWithXPath, but the path argument identifies a
		///// single root node, which contains string elemenents (and optionally others,
		///// which are ignored), each containing id and optionally txt elements.
		///// This is much more efficient than GetStringWithXPath when multiple items
		///// are wanted from the same root node, but only reliable when the path
		///// identifies a single root.
		///// </summary>
		///// <param name="id"></param>
		///// <param name="rootXPathFragment"></param>
		///// <returns></returns>
		//public string GetStringWithRootXPath(string id, string rootXPathFragment)
		//{
		//    id = id.Trim();
		//    Dictionary<string, string> items = null;
		//    if (m_pathsToStrings.ContainsKey(rootXPathFragment))
		//    {
		//        items = m_pathsToStrings[rootXPathFragment];
		//    }
		//    else
		//    {
		//        string path = "strings/" + rootXPathFragment;
		//        if (path[path.Length - 1] == '/')
		//            path = path.Substring(0, path.Length - 1); // strip closing slash.
		//        XmlNode parent = m_document.SelectSingleNode(path);
		//        items = new Dictionary<string, string>();
		//        m_pathsToStrings[rootXPathFragment] = items;
		//        if (parent != null)
		//        {
		//            foreach(XmlNode child in parent.ChildNodes)
		//            {
		//                string idChild = XmlUtils.GetOptionalAttributeValue(child, "id");
		//                //if the txt attribute is missing, use the id attr,
		//                //as this is unacceptable shorthand to use for English entries.
		//                //e.g.: <string id="Anywhere"/> is equivalent to <string id="Anywhere" txt="Anywhere"/>
		//                string txt = XmlUtils.GetOptionalAttributeValue(child, "txt", idChild);
		//                if (child.Name == "string" && idChild != null)
		//                    items[idChild] = txt;
		//            }
		//        }
		//    }
		//    if (items.ContainsKey(id))
		//        return items[id];

		//    if (m_parent != null)
		//        return m_parent.GetStringWithXPath(id, rootXPathFragment);
		//    return "*" + id + "*";
		//}

		///// <summary>
		///// get a string out of the table, specifying an XML path to the group which contains the string
		///// </summary>
		///// <param name="id"></param>
		///// <param name="groupPath">this path should start *underneath* the root <strings/> node.
		/////					e.g. "group[@id='linguistics']/group[@id='phonology']"
		/////	</param>
		///// <returns></returns>
		//public string GetStringWithXPath(string id, string groupXPathFragment)
		//{
		//    id = id.Trim();
		//    XmlNode node = m_document.SelectSingleNode("strings/" + groupXPathFragment + "string[@id='" + id + "']");
		//    if (node == null)
		//    {
		//        if (m_parent != null)
		//        {
		//            return m_parent.GetStringWithXPath(id, groupXPathFragment);
		//        }
		//        //not found
		//        return "*" + id + "*";
		//    }
		//    return GetTxtAtributeValue(node);
		//}

		///// <summary>
		///// get a string which is embedded in a group (or in a group inside of a group...)
		///// </summary>
		///// <example>
		/////		GetStringInGroup("LexMajorEntry", "ClassNames");
		///// </example>
		///// <example>
		/////		get one out of a group within a group:
		/////		GetString("FooLexMajorEntry", "ClassNames/FooNames");
		///// </example>
		///// <param name="id">the ID parameter of the string you want</param>
		///// <param name="groupPath"> e.g. linguistics/morphology
		/////	</param>
		///// <returns></returns>
		//public string GetString(string id, string groupPath)
		//{
		//    return GetStringWithRootXPath(id, GetXPathFragmentFromSimpleNotation(groupPath));
		//}

		///// <summary>
		///// look up a list of string IDs and return an array of strings
		///// </summary>
		///// <example>
		/////		here, the strings will be looked up at the root level
		/////		<stringList ids="anywhere, somewhere to left, somewhere to right, adjacent to left, adjacent to right"/>
		///// </example>
		///// <example>
		/////		here, the strings will be looked up under a nested group
		/////		<stringList group="MoMorphemeAdhocCoProhibition/adjacency" ids="anywhere, somewhere to left, somewhere to right, adjacent to left, adjacent to right"/>
		///// </example>
		///// <param name="node">the name of the node is ignored, only the attributes are read</param>
		///// <returns></returns>
		//public string[] GetStringsFromStringListNode(XmlNode node)
		//{
		//    string ids=XmlUtils.GetManditoryAttributeValue(node, "ids");
		//    string[] idList = ids.Split(new char[]{','});
		//    string[] strings = new string[idList.Length];
		//    string groupPath = "";
		//    string simplePath = XmlUtils.GetOptionalAttributeValue(node, "group");
		//    if (simplePath!= null)
		//    {
		//        groupPath = GetXPathFragmentFromSimpleNotation(simplePath);
		//    }
		//    int i = 0;
		//    foreach(string id in idList)
		//    {
		//        strings[i++] = GetStringWithXPath(id, groupPath);
		//    }
		//    return strings;
		//}

		///// <summary>
		///// creat an XPATH for use with GetString, based on a simpler notation
		///// </summary>
		///// <param name="path">e.g. "linguistics/morphology" </param>
		///// <returns></returns>
		//protected string GetXPathFragmentFromSimpleNotation(string simplePath)
		//{
		//    if(simplePath == "")
		//        return "";
		//    string path = "";
		//    string[] names = simplePath.Split(new char[]{'/'});
		//    foreach(string name in names)
		//    {
		//        path += "group[@id = '" + name.Trim() + "']/";
		//    }
		//    return path;
		//}

		///// <summary>
		///// Retrieve a localized version of the input string if possible, otherwise
		///// return the input string.
		///// </summary>
		///// <param name="sValue"></param>
		///// <returns></returns>
		//public string LocalizeAttributeValue(string sValue)
		//{
		//    if (String.IsNullOrEmpty(sValue))
		//        return sValue;
		//    string sLocValue = this.GetString(sValue, "LocalizedAttributes");
		//    if (String.IsNullOrEmpty(sLocValue))
		//        return sValue;
		//    else if (sLocValue == "*" + sValue.Trim() + "*")
		//        return sValue;
		//    else
		//        return sLocValue;
		//}

		///// <summary>
		///// Retrieve a localized version of the input string if possible, otherwise
		///// return the input string.
		///// </summary>
		///// <param name="sValue"></param>
		///// <returns></returns>
		//public string LocalizeLiteralValue(string sValue)
		//{
		//    if (String.IsNullOrEmpty(sValue))
		//        return sValue;
		//    string sLocValue = this.GetString(sValue, "LocalizedLiterals");
		//    if (String.IsNullOrEmpty(sLocValue))
		//        return sValue;
		//    else if (sLocValue == "*" + sValue.Trim() + "*")
		//        return sValue;
		//    else
		//        return sLocValue;
		//}
	}
}
