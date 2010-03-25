using System.Xml;

namespace SilUtils
{
	public class XmlHelper
	{
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets an attribute's value from the specified node.
		/// </summary>
		/// <param name="node"></param>
		/// <param name="attribute"></param>
		/// <returns>String value of the attribute or null if it cannot be found.</returns>
		/// ------------------------------------------------------------------------------------
		public static string GetAttributeValue(XmlNode node, string attribute)
		{
			if (node == null || node.Attributes[attribute] == null)
				return null;

			return node.Attributes.GetNamedItem(attribute).Value.Trim();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static int GetIntFromAttribute(XmlNode node, string attribute, int defaultValue)
		{
			string val = GetAttributeValue(node, attribute);
			int retVal;
			return (int.TryParse(val, out retVal) ? retVal : defaultValue);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static float GetFloatFromAttribute(XmlNode node, string attribute, float defaultValue)
		{
			string val = GetAttributeValue(node, attribute);
			float retVal;
			return (float.TryParse(val, out retVal) ? retVal : defaultValue);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool GetBoolFromAttribute(XmlNode node, string attribute)
		{
			return GetBoolFromAttribute(node, attribute, false);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public static bool GetBoolFromAttribute(XmlNode node, string attribute, bool defaultValue)
		{
			string val = GetAttributeValue(node, attribute);
			return (val == null ? defaultValue : val.ToLower() == "true");
		}
	}
}
