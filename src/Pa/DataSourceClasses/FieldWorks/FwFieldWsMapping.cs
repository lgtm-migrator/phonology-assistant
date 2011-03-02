﻿using System.Xml.Serialization;

namespace SIL.Pa.DataSource.FieldWorks
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Serialized with an FwDataSourceInfo class. This class maps a writing system to a
	/// FieldWorks field.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	[XmlType("FieldWsInfo")]
	public class FwFieldWsMapping
	{
		[XmlAttribute]
		public string FieldName { get; set; }
	
		[XmlAttribute("WsId")]
		public string WsId { get; set; }

		[XmlAttribute("WsName")]
		public string WsName { get; set; }

		[XmlAttribute("Ws")]
		public int WsHvo { get; set; }

		/// ------------------------------------------------------------------------------------
		public FwFieldWsMapping()
		{
		}

		/// ------------------------------------------------------------------------------------
		public FwFieldWsMapping(string fieldname, int hvo)
		{
			FieldName = fieldname;
			WsHvo = hvo;
		}

		/// ------------------------------------------------------------------------------------
		public FwFieldWsMapping(string fieldname, string id)
		{
			FieldName = fieldname;
			WsId = id;
		}

		/// ------------------------------------------------------------------------------------
		public FwFieldWsMapping(string fieldname, FwWritingSysInfo fwWsInfo)
			: this(fieldname, (fwWsInfo != null ? fwWsInfo.Hvo : 0))
		{
			if (fwWsInfo != null)
			{
				WsId = fwWsInfo.Id;
				WsName = fwWsInfo.Name;
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Makes a deep copy of the FwDataSourceWsInfo object and returns it.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public FwFieldWsMapping Copy()
		{
			return new FwFieldWsMapping(FieldName, WsHvo) { WsId = WsId, WsName = WsName };
		}
	}
}
