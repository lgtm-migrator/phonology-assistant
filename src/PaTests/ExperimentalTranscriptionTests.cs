// ---------------------------------------------------------------------------------------------
#region // Copyright (c) 2005-2015, SIL International.
// <copyright from='2005' to='2015' company='SIL International'>
//		Copyright (c) 2005-2015, SIL International.
//    
//		This software is distributed under the MIT License, as specified in the LICENSE.txt file.
// </copyright> 
#endregion
// 
using System.Collections.Generic;
using NUnit.Framework;
using SIL.Pa.Model;
using SIL.Pa.TestUtils;

namespace SIL.Pa.Tests
{
    /// --------------------------------------------------------------------------------
    /// <summary>
    /// Tests Misc. methods in App.
    /// </summary>
    /// --------------------------------------------------------------------------------
    [TestFixture]
    public class TranscriptionChangesTests : TestBase
	{
		private TranscriptionChanges m_exTransList;

		#region Setup/Teardown
		/// ------------------------------------------------------------------------------------
		[SetUp]
		public void TestSetup()
		{
			m_exTransList = new TranscriptionChanges();

			_prj.TranscriptionChanges.Clear();
			_prj.TranscriptionChanges.AddRange(m_exTransList);
			_prj.AmbiguousSequences.Clear();
			//App.IPASymbolCache.UndefinedCharacters = new UndefinedPhoneticCharactersInfoList();
			_prj.PhoneticParser.LogUndefinedCharactersWhenParsing = false;
		}

		#endregion

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Tests that PA-684 is fixed.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		[Test]
		public void TestWithStressMark()
		{
			List<string> transtToCvtrTo = new List<string>();
			transtToCvtrTo.Add("e");

			TranscriptionChange extrans = new TranscriptionChange();
			extrans.SetReplacementOptions(transtToCvtrTo);
			extrans.ReplaceWith = transtToCvtrTo[0];
			extrans.WhatToReplace = "e\u02D0";
			m_exTransList.Add(extrans);

			string text = m_exTransList.Convert("we\u02D0nop");
			Assert.AreEqual("wenop", text);

			text = m_exTransList.Convert("we\u02D0\u02C8nop");
			Assert.AreEqual("we\u02C8nop", text);
		}
	}
}