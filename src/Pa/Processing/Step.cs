﻿using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace SIL.Pa.Processing
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Corresponds to one xslt element in the Processing.xml file.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class Step
	{
		private readonly string m_xsltFilePath;
		private readonly XslCompiledTransform m_xslt;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// <param name="navigator">The parent pipeline element containing the xslt
		/// elements.</param>
		/// <param name="namespaceManager">In the Processing.xml file, each pipeline and its
		/// children are in the XProc namespace.</param>
		/// <param name="processingFolder">Full path to folder containing Processing.xml
		/// and *.xsl files.</param>
		/// ------------------------------------------------------------------------------------
		public static Step Create(XPathNavigator navigator,
			XmlNamespaceManager namespaceManager, string processingFolder)
		{
			XPathNavigator documentNavigator =
				navigator.SelectSingleNode("p:input[@port='stylesheet']/p:document[@href]", namespaceManager);
			
			if (documentNavigator == null)
				return null;

			var xsltFileName = documentNavigator.GetAttribute("href", string.Empty); // No namespace for attributes.
			var xsltFilePath = Path.Combine(processingFolder, xsltFileName);
			var xslt = new XslCompiledTransform(true);

			try
			{
				// TO DO: If you enable the document() function, restrict the resources that can be accessed
				// by passing an XmlSecureResolver object to the Transform method.
				// The XmlResolver used to resolve any style sheets referenced in XSLT import and include elements.
				XsltSettings settings = new XsltSettings(true, false);
				xslt.Load(xsltFilePath, settings, null);
			}
			catch (Exception e)
			{
				Exception exception = new Exception("Unable to build XSL Transformation filter.", e);
				exception.Data.Add("XSL Transformation file path", xsltFilePath);
				throw exception;
			}

			return new Step(xsltFilePath, xslt);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private Step (string xsltFilePath, XslCompiledTransform xslt)
		{
			m_xsltFilePath = xsltFilePath;
			m_xslt = xslt;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Given inputStream and outputStream, do one XSL Transformation step. 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public MemoryStream Transform(MemoryStream inputStream)
		{
			var readerSettings = new XmlReaderSettings();
			readerSettings.ProhibitDtd = false; // If true, it throws an exception if there is a DTD.
			readerSettings.ValidationType = ValidationType.None;
			var inputXML = XmlReader.Create(inputStream, readerSettings);

			// OutputSettings corresponds to the xsl:output element of the style sheet.
			var writerSettings = m_xslt.OutputSettings.Clone();
			if (writerSettings.Indent) // Cause indent="true" to insert line breaks but not tabs.
				writerSettings.IndentChars = string.Empty; // Even if the document element is html.

			var outputStream = new MemoryStream();
			var outputXML = XmlWriter.Create(outputStream, writerSettings);
			try
			{
				m_xslt.Transform(inputXML, outputXML);
			}
			catch (Exception e)
			{
				var exception = new Exception("Unable to convert using XSL Transformation filter.", e);
				exception.Data.Add("XSL Transformation file path", m_xsltFilePath);
				throw exception;
			}
			finally
			{
				inputXML.Close();
				outputXML.Flush(); // The next filter or the data sink will close the stream
			}

			outputStream.Flush();
			return outputStream;
		}
	}
}
