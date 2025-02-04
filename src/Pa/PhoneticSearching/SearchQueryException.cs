// ---------------------------------------------------------------------------------------------
#region // Copyright (c) 2005-2015, SIL International.
// <copyright from='2005' to='2015' company='SIL International'>
//		Copyright (c) 2005-2015, SIL International.
//    
//		This software is distributed under the MIT License, as specified in the LICENSE.txt file.
// </copyright> 
#endregion
// 
using System;
using System.Text;
using SilTools;

namespace SIL.Pa.PhoneticSearching
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// An exception class for cells in an XY chart that caused an error when their search
	/// was performed.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class SearchQueryException : Exception
	{
		public Exception ThrownException { get; private set; }
		public string SearchQueryPattern { get; private set; }

		///// ------------------------------------------------------------------------------------
		//public SearchQueryException(SearchQuery query)
		//{
		//    if (query.Errors.Count > 0)
		//    {
		//        var fmt = App.GetString(
		//            "SearchQuery.ErrListMsg", "{0}) {1}\n\n",
		//            "This is a format string for a number list of error messages for a search query.");

		//        var errors = new StringBuilder();
		//        for (int i = 0; i < query.Errors.Count; i++)
		//        {
		//            var errorMsg = query.Errors[i];

		//            //if (errorMsg.StartsWith(SearchEngine.kBracketingError))
		//            //{
		//            //    int indexOfColon = errorMsg.IndexOf(":");
		//            //    errorMsg = string.Format(App.GetString("SearchQuery.InvalidTextInBracketsPopupErrorMsg",
		//            //        "Invalid text '{0}' between square brackets."), errorMsg.Substring(indexOfColon + 1));
		//            //}

		//            errors.AppendFormat(fmt, i + 1, errorMsg);
		//        }
				
		//        m_queryErrorMsg = errors.ToString();
		//    }
		//    else
		//    {
		//        SearchQuery modifiedQuery;
		//        if (!App.ConvertClassesToPatterns(query, out modifiedQuery, false, out m_queryErrorMsg))
		//            return;

		//        var engine = new SearchEngine(modifiedQuery);

		//        if (engine.GetWordBoundaryCondition() != SearchEngine.WordBoundaryCondition.NoCondition)
		//            m_queryErrorMsg = WordBoundaryError;
		//        else if (engine.GetZeroOrMoreCondition() != SearchEngine.ZeroOrMoreCondition.NoCondition)
		//            m_queryErrorMsg = ZeroOrMoreError;
		//        else if (engine.GetOneOrMoreCondition() != SearchEngine.OneOrMoreCondition.NoCondition)
		//            m_queryErrorMsg = OneOrMoreError;
		//    }

		//    if (string.IsNullOrEmpty(m_queryErrorMsg))
		//        m_queryErrorMsg = "Unknown Error.";

		//    m_queryErrorMsg = Utils.ConvertLiteralNewLines(m_queryErrorMsg);
		//    m_queryErrorMsg = m_queryErrorMsg.TrimEnd();
		//}

		/// ------------------------------------------------------------------------------------
		public SearchQueryException(Exception thrownException, SearchQuery query)
		{
			ThrownException = thrownException;
			SearchQueryPattern = query.Pattern;
		}

		///// ------------------------------------------------------------------------------------
		//public static string WordBoundaryError
		//{
		//    get
		//    {
		//        return App.GetString("SearchQuery.WordBoundaryError",
		//                "The space/word boundary symbol (#) may not be the first or last item in the search item portion (what precedes the slash) of the search pattern. Please correct this and try your search again.");
		//    }
		//}

		///// ------------------------------------------------------------------------------------
		//public static string ZeroOrMoreError
		//{
		//    get
		//    {
		//        return App.GetString("SearchQuery.ZeroOrMoreError",
		//            "The zero-or-more symbol (*) was found in an invalid location within the search pattern. The zero-or-more symbol may only be the first item in the preceding environment and/or the last item in the environment after. Please correct this and try your search again.");
		//    }
		//}

		///// ------------------------------------------------------------------------------------
		//public static string OneOrMoreError
		//{
		//    get
		//    {
		//        return App.GetString("SearchQuery.OneOrMoreError",
		//            "The one-or-more symbol (+) was found in an invalid location within the search pattern. The one-or-more symbol may only be the first item in the preceding environment and/or the last item in the environment after. Please correct this and try your search again.");
		//    }
		//}

		///// ------------------------------------------------------------------------------------
		///// <summary>
		///// Gets the error generated by the search engine.
		///// </summary>
		///// ------------------------------------------------------------------------------------
		//public string QueryErrorMessage
		//{
		//    get { return m_queryErrorMsg; }
		//}
	}
}
