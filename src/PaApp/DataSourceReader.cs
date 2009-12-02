using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using SIL.Pa.Data;
using SIL.Pa.Resources;
using SilUtils;
using SIL.SpeechTools.Utils;

namespace SIL.Pa
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Importer class parses through an SF file and sends Records to Import
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class DataSourceReader
	{
		protected RecordCache m_recCache;
		protected RecordCacheEntry m_recCacheEntry;
		protected PaProject m_project;
		protected PaDataSource m_currDataSource;
		protected List<PaDataSource> m_dataSources;
		protected List<SFMarkerMapping> m_mappings;
		protected Dictionary<string, List<string>> m_fieldsForMarkers;
		protected List<string> m_interlinearFields;
		protected int m_totalLinesToRead = 0;
		protected ToolStripProgressBar m_progressBar;

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Constructs a data source reader object.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public DataSourceReader(PaProject project)
		{
			m_project = project;
			m_dataSources = new List<PaDataSource>();
			CheckNeedForSQLServer(project.DataSources);

			foreach (PaDataSource source in project.DataSources)
			{
				if (source.FwSourceDirectFromDB)
				{
					if (!source.SkipLoading)
						CheckExistenceOfFWDatabase(source);
				}
				else if (!File.Exists(source.DataSourceFile))
				{					
					string newPath = GetMissingDataSourceAction(source.DataSourceFile);
					if (newPath == null)
					{
						source.SkipLoading = true;
						continue;
					}

					source.DataSourceFile = newPath;
					project.Save();
				}
				
				if (source.DataSourceType != DataSourceType.XML &&
					source.DataSourceType != DataSourceType.Unknown && !source.SkipLoading)
				{
					m_dataSources.Add(source);
					m_totalLinesToRead += source.TotalLinesInFile;
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Cycles through the data sources, checking if any are sources directly from a
		/// FW database. If so, then an attempt is made to start SQL server if it isn't
		/// already started.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void CheckNeedForSQLServer(IEnumerable<PaDataSource> dataSources)
		{
			bool alreadyTriedToStartSQLServer = false;

			foreach (PaDataSource source in dataSources)
			{
				if (source.FwSourceDirectFromDB)
				{
					if (!alreadyTriedToStartSQLServer && FwDBUtils.StartSQLServer(true))
						return;

					alreadyTriedToStartSQLServer = true;
					source.SkipLoading = true;
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Verifies the existence of the specified FW data source.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void CheckExistenceOfFWDatabase(PaDataSource datasource)
		{
			if (datasource == null)
				return;

			if (!FwDBUtils.IsSQLServerStarted)
			{
				if (!FwDBUtils.StartSQLServer(true))
					return;
			}

			if (datasource.FwDataSourceInfo != null)
			{
				FwDataSourceInfo[] fwDBInfoList =
					FwDBUtils.GetFwDataSourceInfoList(datasource.FwDataSourceInfo.MachineName, false);

				if (fwDBInfoList != null)
				{
					foreach (FwDataSourceInfo fwDBInfo in fwDBInfoList)
					{
						if (datasource.FwDBName == fwDBInfo.DBName)
							return;
					}
				}

				datasource.FwDataSourceInfo.IsMissing = true;
			}

			MissingFWDatabaseMsgBox.ShowDialog(datasource.ToString(true));
			datasource.SkipLoading = true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Returns null if the user wants to skip loading a missing data source or a full
		/// file path the user specified as the relocated data source file.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private string GetMissingDataSourceAction(string dataSourceFile)
		{
			if (MissingDataSourceMsgBox.ShowDialog(dataSourceFile) == DialogResult.Cancel)
				return null;
		
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.CheckFileExists = true;
			dlg.CheckPathExists = true;
			dlg.FileName = Path.GetFileName(dataSourceFile);
			dlg.Filter = ResourceHelper.GetString("kstidFileTypeAllFiles");
			dlg.ShowReadOnly = false;
			dlg.Title =	Properties.Resources.kstidMissingDataSourceOFDMsg;
			dlg.InitialDirectory = Path.GetFullPath(dataSourceFile);

			while (dlg.ShowDialog() == DialogResult.Cancel)
			{
				if (MissingDataSourceMsgBox.ShowDialog(dataSourceFile) == DialogResult.Cancel)
					return null;
			}

			return dlg.FileName;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Read each data source file into the record cache.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public WordCache Read()
		{
			if (PaApp.RecordCache != null)
				PaApp.RecordCache.Dispose();

			Application.DoEvents();
			TempRecordCache.Dispose();
			m_recCache = new RecordCache();
			PaApp.RecordCache = m_recCache;
			PaApp.InitializeProgressBar(string.Empty, m_totalLinesToRead);
			DataUtils.IPACharCache.UndefinedCharacters = new UndefinedPhoneticCharactersInfoList();

			foreach (PaDataSource source in m_dataSources)
			{
				DataUtils.IPACharCache.UndefinedCharacters.CurrentDataSourceName =
					(source.DataSourceType == DataSourceType.FW && source.FwDataSourceInfo != null ?
					source.FwDataSourceInfo.ToString() : Path.GetFileName(source.DataSourceFile));

				DataUtils.IPACharCache.LogUndefinedCharactersWhenParsing = true;

				m_currDataSource = source;

				PaApp.UpdateProgressBarLabel(string.Format(
					Properties.Resources.kstidReadingDataSourceProgressLabel,
					(source.FwSourceDirectFromDB && source.FwDataSourceInfo != null ?
					source.FwDataSourceInfo.DBName : Path.GetFileName(source.DataSourceFile))));

				StreamReader reader = null;

				try
				{
					PaApp.MsgMediator.SendMessage("BeforeReadingDataSource", source);
					bool readSuccess = true;

					switch (source.DataSourceType)
					{
						case DataSourceType.Toolbox:
						case DataSourceType.SFM:
							source.LastModification = File.GetLastWriteTimeUtc(source.DataSourceFile);
							reader = new StreamReader(source.DataSourceFile);
							m_mappings = source.SFMappings;
							BuildListOfFieldsForMarkers();
							readSuccess = ReadSFMFile(reader);
							break;

						case DataSourceType.SA:
							ReadSASource(source.DataSourceFile);
							break;

						case DataSourceType.XML:
							source.LastModification = File.GetLastWriteTimeUtc(source.DataSourceFile);
							break;

						case DataSourceType.FW:
						case DataSourceType.PAXML:
							if (source.FwSourceDirectFromDB)
								ReadFwDataSource();
							else
							{
								source.LastModification = File.GetLastWriteTimeUtc(source.DataSourceFile);
								ReadPaXMLFile();
							}
							break;
					}

					if (readSuccess)
						PaApp.MsgMediator.SendMessage("AfterReadingDataSource", source);
					else
					{
						string msg =
							string.Format(Properties.Resources.kstidErrorProcessingDataSourceFile,
							SilUtils.Utils.PrepFilePathForSTMsgBox(source.DataSourceFile));

						SilUtils.Utils.MsgBox(msg, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
						PaApp.MsgMediator.SendMessage("AfterReadingDataSourceFailure", source);
					}
				}
				catch (Exception e)
				{
					string msg = string.Format(
						Properties.Resources.kstidErrorReadingDataSourceFile,
						SilUtils.Utils.PrepFilePathForSTMsgBox(source.DataSourceFile), e.Message);

					SilUtils.Utils.MsgBox(msg, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				}
				finally
				{
					if (reader != null)
						reader.Close();
				}
			}

			PaApp.InitializeProgressBar(Properties.Resources.kstidParsingDataMsg, m_recCache.Count);
			m_recCache.BuildWordCache(PaApp.ProgressBar);
			DataUtils.IPACharCache.LogUndefinedCharactersWhenParsing = false;
			PaApp.IncProgressBar();
			TempRecordCache.Save();
			PaApp.UninitializeProgressBar();

			return (m_recCache.WordCache.Count == 0 ? null : m_recCache.WordCache);
		}

		#region PaXML/FW data source reading
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void ReadFwDataSource()
		{
			PaApp.IncProgressBar();

			if (m_currDataSource.FwDataSourceInfo != null)
			{
				// Get the lexical data from the FW database.
				FwDataReader fwReader = new FwDataReader(m_currDataSource.FwDataSourceInfo);
				m_currDataSource.SkipLoading = !fwReader.GetData(HandleReadingFwData);
				m_currDataSource.FwDataSourceInfo.IsMissing = m_currDataSource.SkipLoading;
			
				if (!m_currDataSource.SkipLoading && !m_currDataSource.FwDataSourceInfo.IsMissing)
					m_currDataSource.FwDataSourceInfo.UpdateLastModifiedStamp();
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// 
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void HandleReadingFwData(SqlDataReader reader)
		{
			if (reader == null || reader.IsClosed)
				return;

			// First, get a list of the fields returned from the query
			// and translate those to their corresponding PA fields.
			List<string> fieldNames = new List<string>();
			for (int i = 0; i < reader.FieldCount; i++)
			{
				PaFieldInfo fieldInfo =
					m_project.FieldInfo.GetFieldFromFwQueryFieldName(reader.GetName(i));

				fieldNames.Add(fieldInfo != null ? fieldInfo.FieldName : null);
			}

			while (reader.Read())
			{
				// Make a new record entry.
				m_recCacheEntry = new RecordCacheEntry(false);
				m_recCacheEntry.DataSource = m_currDataSource;
				m_recCacheEntry.NeedsParsing = false;
				m_recCacheEntry.WordEntries = new List<WordCacheEntry>();

				// Make a new word entry because for FW data sources read directly from the
				// database, there will be a one-to-one correspondence between record cache
				// entries and word cache entries.
				WordCacheEntry wentry = new WordCacheEntry(m_recCacheEntry, true);

				// Read the data for all columns. If there are columns the record
				// or word entries don't recognize, they'll just be ignored.
				for (int i = 0; i < fieldNames.Count; i++)
				{
					if (!(reader[i] is DBNull) && fieldNames[i] != null)
					{
						m_recCacheEntry.SetValue(fieldNames[i], reader[i].ToString());
						wentry.SetValue(fieldNames[i], reader[i].ToString());

						if (fieldNames[i] == m_project.FieldInfo.AudioFileField.FieldName)
						{
							string lengthField = m_project.FieldInfo.AudioFileLengthField.FieldName;
							string offsetField = m_project.FieldInfo.AudioFileOffsetField.FieldName;

							if (wentry[lengthField] == null)
								wentry.SetValue(lengthField, "0");
	
							if (wentry[offsetField] == null)
								wentry.SetValue(offsetField, "0");
						}
					}
				}

				// Add the entries to the caches.
				m_recCacheEntry.WordEntries.Add(wentry);
				m_recCache.Add(m_recCacheEntry);
			}

			m_currDataSource.LastModification = DateTime.Now;
			m_currDataSource.FwDataSourceInfo.UpdateLastModifiedStamp();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Reads a PA XML file into the record cache.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void ReadPaXMLFile()
		{
			RecordCache cache = RecordCache.Load(m_currDataSource);
			if (cache == null)
				return;

			AddCustomFieldsFromPaXML(cache);

			// If the record cache member variable currently points to an empty cache, then
			// just set it to point to the cache into which we just read the specified PaXML
			// file. Otherwise move the records from the cache we just read into to the
			// member variable cache.
			if (m_recCache.Count == 0)
			{
				PaApp.IncProgressBar(cache.Count);
				m_recCache = cache;
			}
			else
			{
				while (cache.Count > 0)
				{
					PaApp.IncProgressBar();
					m_recCache.Add(cache[0]);
					cache.RemoveAt(0);
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Make sure custom fields stored in the PaXML file are added to the project's field
		/// list.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void AddCustomFieldsFromPaXML(RecordCache cache)
		{
			if (cache.DeserializedCustomFields != null &&
				cache.DeserializedCustomFields.Count > 0)
			{
				foreach (PaFieldInfo customField in cache.DeserializedCustomFields)
				{
					PaFieldInfo fieldInfo = m_project.FieldInfo[customField.FieldName];
					if (fieldInfo == null)
						m_project.FieldInfo.Add(customField);
				}

				cache.DeserializedCustomFields = null;
			}
		}

		#endregion

		#region SA data source reading
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Reads an SA sound file data source.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void ReadSASource(string audioFile)
		{
			// At this point, we know we have an SA data source to read so set the help
			// file and help topic for the encoding conversion dialog's help button.
			// That dialog is common to PA and SA, so take special treatment (i.e. not
			// the way all the other PA dialog's help buttons are handled). Set the
			// encoding conversion dialog's static variables so the proper help file and
			// topic are jumped to if the user is presented with that dialog and chooses
			// to click its help button.
			TransConvertersDlg.HelpFilePath = PaApp.HelpFilePath;
			TransConvertersDlg.HelpTopic = ResourceHelper.GetHelpString("hidTransConvertersDlg");

			m_currDataSource.LastModification =
				SaAudioDocument.GetTranscriptionFileModifiedTime(audioFile);

			SaAudioDocumentReader reader = new SaAudioDocumentReader();
			if (!reader.Initialize(audioFile))
				return;

			SortedDictionary<uint, AudioDocWords> adWords = reader.Words;
			if (adWords == null)
				return;
			
			// Make only a single record entry for the entire wave file.
			m_recCacheEntry = new RecordCacheEntry(false);
			m_recCacheEntry.DataSource = m_currDataSource;
			m_recCacheEntry.NeedsParsing = false;
			m_recCacheEntry.Channels = reader.Channels;
			m_recCacheEntry.BitsPerSample = reader.BitsPerSample;
			m_recCacheEntry.SamplesPerSecond = reader.SamplesPerSecond;
			ReadRecLevelSaFields(reader);

			PaFieldInfo fieldInfo = PaApp.FieldInfo.AudioFileField;
			if (fieldInfo != null)
				m_recCacheEntry.SetValue(fieldInfo.FieldName, audioFile);

			PaApp.IncProgressBar();
			int wordIndex = 0;
			List<WordCacheEntry> wordEntries = new List<WordCacheEntry>();

			// Go through each word, adding a word cache entry for each.
			foreach (KeyValuePair<uint, AudioDocWords> adw in adWords)
			{
				WordCacheEntry wentry = new WordCacheEntry(m_recCacheEntry, wordIndex++, true);
				
				fieldInfo = PaApp.FieldInfo.PhoneticField;
				if (fieldInfo != null)
					wentry[fieldInfo.FieldName] = adw.Value.Phonetic;

				fieldInfo = PaApp.FieldInfo.PhonemicField;
				if (fieldInfo != null)
					wentry[fieldInfo.FieldName] = adw.Value.Phonemic;

				fieldInfo = PaApp.FieldInfo.ToneField;
				if (fieldInfo != null)
					wentry[fieldInfo.FieldName] = adw.Value.Tone;

				fieldInfo = PaApp.FieldInfo.OrthoField;
				if (fieldInfo != null)
					wentry[fieldInfo.FieldName] = adw.Value.Orthographic;

				fieldInfo = PaApp.FieldInfo.GlossField;
				if (fieldInfo != null)
					wentry[fieldInfo.FieldName] = adw.Value.Gloss;

				fieldInfo = PaApp.FieldInfo.ReferenceField;
				if (fieldInfo != null)
					wentry[fieldInfo.FieldName] = adw.Value.Reference;

				fieldInfo = PaApp.FieldInfo.AudioFileLengthField;
				if (fieldInfo != null)
					wentry[fieldInfo.FieldName] = adw.Value.AudioLength.ToString();

				fieldInfo = PaApp.FieldInfo.AudioFileOffsetField;
				if (fieldInfo != null)
					wentry[fieldInfo.FieldName] = adw.Key.ToString();

				wordEntries.Add(wentry);
			}

			m_recCacheEntry.WordEntries = wordEntries;
			m_recCache.Add(m_recCacheEntry);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Reads misc. fields from the SA data source that are stored in a record cache entry.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void ReadRecLevelSaFields(SaAudioDocumentReader reader)
		{
			if (m_project.FieldInfo == null)
				return;

			BindingFlags flags = BindingFlags.GetProperty | BindingFlags.Instance |
				BindingFlags.Public;
			
			foreach (PaFieldInfo fieldInfo in m_project.FieldInfo)
			{
				if (string.IsNullOrEmpty(fieldInfo.SaFieldName))
					continue;

				try
				{
					object value = typeof(SaAudioDocumentReader).InvokeMember(
						fieldInfo.SaFieldName, flags, null, reader, null);

					if (value != null)
						m_recCacheEntry.SetValue(fieldInfo.FieldName, value.ToString());
				}
				catch { }
			}
		}

		#endregion

		#region SFM file reading methods
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Builds a list of mappings and all the fields that are mapped to those markers.
		/// Also, a list of all the fields marked as interlinear are saved.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void BuildListOfFieldsForMarkers()
		{
			m_fieldsForMarkers = new Dictionary<string, List<string>>();
			m_interlinearFields = new List<string>();

			foreach (SFMarkerMapping mapping in m_mappings)
			{
				if (mapping.FieldName != PaDataSource.kRecordMarker && !string.IsNullOrEmpty(mapping.Marker))
				{
					if (!m_fieldsForMarkers.ContainsKey(mapping.Marker))
						m_fieldsForMarkers[mapping.Marker] = new List<string>();

					m_fieldsForMarkers[mapping.Marker].Add(mapping.FieldName);

					if (mapping.IsInterlinear)
						m_interlinearFields.Add(mapping.FieldName);
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Reads a single SFM file
		/// </summary>
		/// <returns>True indicates success</returns>
		/// ------------------------------------------------------------------------------------
		protected bool ReadSFMFile(StreamReader file)
		{
			string recMarker = GetRecordMarkerSFM(m_currDataSource);
			if (string.IsNullOrEmpty(recMarker))
				return false;

			string currLine;
			StringBuilder field = new StringBuilder();
			bool onFirstRecordMarker = false;
			bool foundFirstRecord = false;
			m_recCacheEntry = null;

			while ((currLine = file.ReadLine()) != null) 
			{
				PaApp.IncProgressBar();
				currLine = currLine.Trim();
				string currMarker = null;

				if (currLine.StartsWith("\\"))
				{
					string[] split = currLine.Split(" ".ToCharArray(), 2);
					if (split.Length >= 1)
						currMarker = split[0];
				}

				// Ignore all data up to the first record.
				if (!foundFirstRecord)
				{
					if (currMarker != recMarker)
						continue;

					foundFirstRecord = true;
					onFirstRecordMarker = true;
				}

				// If the current line doesn't start a new field, then add it to the
				// previous line's data.
				if (!currLine.StartsWith("\\"))
				{
					field.Append(" " + currLine);
					continue;
				}

				// At this point, if the string builder contains any data, we know we've
				// finished reading the data for a single field. Therefore, save the field's
				// data.
				if (field.Length > 0)
					ParseAndStoreSFMField(field.ToString());

				// Check if we've come to the beginning of a record. If so, then make sure
				// to save the contents of the previous record and begin a new one.
				if (currMarker == recMarker)
				{
					if (onFirstRecordMarker)
						onFirstRecordMarker = false;
					else if (m_recCacheEntry != null)
					{
						m_recCache.Add(m_recCacheEntry);
						m_recCacheEntry = null;
					}
				}

				// Prepare to start a new field's data accumulation and add the line
				// just read from the file.
				field.Length = 0;
				field.Append(currLine);
			}

			// Process the final field in the file
			if (field.Length > 0)
				ParseAndStoreSFMField(field.ToString());
			
			// Save the final record in the file
			if (m_recCacheEntry != null)
			{
				m_recCache.Add(m_recCacheEntry);
				m_recCacheEntry = null;
			}

			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the marker that was mapped as the record marker. This is used to determine
		/// where record breaks are.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private string GetRecordMarkerSFM(PaDataSource source)
		{
			foreach (SFMarkerMapping mapping in source.SFMappings)
			{
				if (mapping.FieldName == PaDataSource.kRecordMarker)
					return mapping.Marker;
			}

			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Adds to the record cache the data for a single field read from an SFM data source
		/// record.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private void ParseAndStoreSFMField(string field)
		{
			if (field == null)
				return;

			field = field.Trim();

			if (!field.StartsWith("\\"))
				return;

			// Get two strings, the first containing the backslash marker
			// and the other contianing the data following the marker.
			string[] split = field.Split(" ".ToCharArray(), 2);

			// If we didn't get two strings back then ignore this field.
			if (split.Length != 2)
				return;

			// If there's no mapping for the marker, ignore this field.
			if (!m_fieldsForMarkers.ContainsKey(split[0]))
				return;

			if (m_recCacheEntry == null)
			{
				m_recCacheEntry = new RecordCacheEntry(true);
				m_recCacheEntry.NeedsParsing = true;
				m_recCacheEntry.DataSource = m_currDataSource;
				m_recCacheEntry.FirstInterlinearField = m_currDataSource.FirstInterlinearField;
				m_recCacheEntry.InterlinearFields = m_interlinearFields;
			}

			PaFieldInfo audioFilefieldInfo = m_project.FieldInfo.AudioFileField;
			PaFieldInfo offsetFieldInfo = m_project.FieldInfo.AudioFileOffsetField;
			PaFieldInfo lengthFieldInfo = m_project.FieldInfo.AudioFileLengthField;

			foreach (string fld in m_fieldsForMarkers[split[0]])
			{
				if (audioFilefieldInfo == null || audioFilefieldInfo.FieldName != fld)
				{
					m_recCacheEntry.SetValue(fld, split[1]);
					continue;
				}

				long startPoint;
				long endPoint;
				string audioFileName = ParseSoundFileName(split[1], out startPoint, out endPoint);

				if (!string.IsNullOrEmpty(audioFileName))
				{
					m_recCacheEntry.SetValue(audioFilefieldInfo.FieldName, audioFileName);

					if (offsetFieldInfo != null && lengthFieldInfo != null)
					{
						long length = endPoint - startPoint;
						m_recCacheEntry.SetValue(offsetFieldInfo.FieldName, startPoint.ToString());
						m_recCacheEntry.SetValue(lengthFieldInfo.FieldName, length.ToString());
					}
				}
			}

			return;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Parse the sound file name returning the file name and beginning / ending points
		/// for playing, if any.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private string ParseSoundFileName(string fileName, out long startPoint, out long endPoint)
		{
			startPoint = 0L;
			endPoint = 0L;

			// Handle null
			if (fileName == null)
				return string.Empty;

			// Handle empty string
			fileName = fileName.Trim();
			if (fileName == string.Empty)
				return string.Empty;

			// Check if the end of the file name string is one of the common audio file types.
			// If so, there's no sense in checking for start and end points since the entire
			// string ends with a file extension.
			foreach (string ext in new string[] {".wav", ".mp3", ".wma", ".ogg", ".ram", ".aif", ".au", ".voc"})
			{
				if (fileName.ToLower().EndsWith(ext))
					return fileName;
			}

			// Find the last space in the file name string. If one cannot be
			// found then we know there is no start and stop point.
			int iSpace = fileName.LastIndexOf(' ');
			if (iSpace < 0)
				return fileName;

			// Remove everything following the last space and assume it's a numeric value
			// indicating a start or stop point (in seconds) in the audio file.
			string sTime2 = fileName.Substring(iSpace);
			
			// Try to convert the value to a numeric.
			float fTime2;
			if (!SilUtils.Utils.TryFloatParse(sTime2, out fTime2))
				return fileName;

			// Remove from the file name string the text removed that represents a numeric value.
			fileName = fileName.Substring(0, iSpace);

			// Find the last space in the file name string. If one cannot be
			// found then we know there is only a start point and stop point.
			iSpace = fileName.LastIndexOf(' ');
			if (iSpace < 0)
			{
				// Assume the time was specified in seconds. We want it in milliseconds.
				startPoint = (long)(Math.Ceiling(fTime2 * 1000f));
				return fileName;
			}

			// Remove everything following the last space and assume it's a numeric value
			// indicating a start point (in seconds) in the audio file.
			string sTime1 = fileName.Substring(iSpace);

			// Try to convert the value to a numeric.
			float fTime1;
			if (!SilUtils.Utils.TryFloatParse(sTime1, out fTime1))
			{
				// Assume the time was specified in seconds. We want it in milliseconds.
				// The parse attempt failed so just make the start and end times the same.
				startPoint = (long)(Math.Ceiling(fTime2 * 1000f));
				return fileName;
			}

			// Assume the times are specified in seconds. We want it in milliseconds.
			startPoint = (long)(Math.Ceiling(fTime1 * 1000f));
			endPoint = (long)(Math.Ceiling(fTime2 * 1000f));

			// Remove from the file name string the text removed that represents the start point.
			return fileName.Substring(0, iSpace);
		}

		#endregion
	}  
}