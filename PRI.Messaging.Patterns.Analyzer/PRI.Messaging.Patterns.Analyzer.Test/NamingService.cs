#if SUPPORT_NAMING_SERVICE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
// ReSharper disable InconsistentNaming
// ReSharper disable UseStringInterpolation
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

namespace StyleCop.Spelling
{
	/// <summary>
	/// The naming service.
	/// </summary>
	public class NamingService : IDisposable
	{
		#region Static Fields

		private static readonly Dictionary<string, NamingService> ServiceCache = new Dictionary<string, NamingService>();

		private static readonly object ServiceCacheLock = new object();

		private static NamingService defaultNamingService;

		#endregion

		#region Fields

		private readonly CultureInfo culture;

		private IDictionary<string, string> alternatesForDeprecatedWords;

		private IDictionary<string, string> casingExceptions;

		private IDictionary<string, string> compoundAlternatesForDiscreteWords;

		private int customDictionaryHashCode;

		private ICollection<string> dictionaryFolders;

		private IDictionary<string, string> discreteWordExceptions;

		private SpellChecker spellChecker;

		#endregion

		#region Constructors and Destructors

		private NamingService(CultureInfo culture)
		{
			this.culture = culture;

			if (StyleCopCore.PlatformID == PlatformID.Win32NT)
			{
				spellChecker = SpellChecker.FromCulture(culture);
			}

			InitCustomDictionaries();
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets the default naming service.
		/// </summary>
		public static NamingService DefaultNamingService
		{
			get
			{
				return defaultNamingService ?? (defaultNamingService = GetNamingService(System.Threading.Thread.CurrentThread.CurrentCulture));
			}
		}

		/// <summary>
		/// Gets the culture.
		/// </summary>
		public CultureInfo Culture
		{
			get
			{
				return culture;
			}
		}

		/// <summary>
		/// Gets an array of the dictionary folders which have been scanned for dictionaries.
		/// </summary>
		public ICollection<string> DictionaryFolders
		{
			get
			{
				return dictionaryFolders.ToArray();
			}
		}

		/// <summary>
		/// Gets a value indicating whether supports spelling.
		/// </summary>
		public bool SupportsSpelling
		{
			get
			{
				return spellChecker != null;
			}
		}

		#endregion

		#region Properties

		internal bool IsEnglishCulture
		{
			get
			{
				return culture.Name.Equals("en", StringComparison.OrdinalIgnoreCase) || culture.Parent.Name.Equals("en", StringComparison.OrdinalIgnoreCase);
			}
		}

		#endregion

		#region Public Methods and Operators

		/// <summary>
		/// Clears the cached services.
		/// </summary>
		public static void ClearCachedServices()
		{
			lock (ServiceCacheLock)
			{
				foreach (NamingService service in ServiceCache.Values)
				{
					service.Dispose();
				}

				ServiceCache.Clear();
				defaultNamingService = null;
			}
		}

		/// <summary>
		/// Gets a naming service for the specified culture.
		/// </summary>
		/// <param name="culture">
		/// The culture to use.
		/// </param>
		/// <returns>
		/// The NamingService for the culture.
		/// </returns>
		[SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "OK here.")]
		public static NamingService GetNamingService(CultureInfo culture)
		{
			if (culture == null)
			{
				throw new ArgumentNullException("culture");
			}

			lock (ServiceCacheLock)
			{
				NamingService service;
				if (!ServiceCache.TryGetValue(culture.Name, out service))
				{
					service = new NamingService(culture);
					ServiceCache[culture.Name] = service;
				}

				return service;
			}
		}

		/// <summary>
		/// Adds the deprecated words dictionary and their preferred alternatives to the list of current deprecated words.
		/// </summary>
		/// <param name="deprecatedWords">
		/// The dictionary of words to add.
		/// </param>
		public void AddDeprecatedWords(IDictionary<string, string> deprecatedWords)
		{
			if (deprecatedWords != null)
			{
				foreach (KeyValuePair<string, string> deprecatedWord in deprecatedWords)
				{
					if (!alternatesForDeprecatedWords.ContainsKey(deprecatedWord.Key))
					{
						alternatesForDeprecatedWords.Add(deprecatedWord);
					}
				}
			}
		}

		/// <summary>
		/// Adds a folder to the list of folders scanned for CustomDictionary.xml files.
		/// </summary>
		/// <param name="path">
		/// The path to add.
		/// </param>
		public void AddDictionaryFolder(string path)
		{
			if (!string.IsNullOrEmpty(path))
			{
				string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));

				if (Directory.Exists(fullPath))
				{
					if (!dictionaryFolders.Contains(fullPath, StringComparer.InvariantCultureIgnoreCase))
					{
						dictionaryFolders.Add(fullPath);
						ScanAndLoadDictionaries(fullPath);
					}
				}
			}
		}

		/// <summary>
		/// Check spelling of the word provided.
		/// </summary>
		/// <param name="word">
		/// The word to check.
		/// </param>
		/// <returns>
		/// The StyleCop.Spelling.WordSpelling.
		/// </returns>
		public WordSpelling CheckSpelling(string word)
		{
			if (!SupportsSpelling)
			{
				throw new InvalidOperationException();
			}

			return spellChecker.Check(word);
		}

		/// <summary>
		/// The dispose.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// The get compound alternate for discrete word.
		/// </summary>
		/// <param name="word">
		/// The word.
		/// </param>
		/// <returns>
		/// The System.String.
		/// </returns>
		public string GetCompoundAlternateForDiscreteWord(string word)
		{
			if (word == null)
			{
				throw new ArgumentNullException("word");
			}

			string str = compoundAlternatesForDiscreteWords[word];
			if ((str != null) && char.IsLower(word[0]))
			{
				str = char.ToLower(str[0], culture) + str.Substring(1);
			}

			return str;
		}

		/// <summary>
		/// Returns a hash code of the files we use to check spelling.
		/// </summary>
		/// <returns>The hash code or 0 if we don't use any other files.</returns>
		public int GetDependantFilesHashCode()
		{
			return SupportsSpelling ? spellChecker.GetDependantFilesHashCode() ^ customDictionaryHashCode : 0;
		}

		/// <summary>
		/// The get discrete alternate for compound word.
		/// </summary>
		/// <param name="word1">
		/// The word 1.
		/// </param>
		/// <param name="word2">
		/// The word 2.
		/// </param>
		/// <returns>
		/// The System.String.
		/// </returns>
		public string GetDiscreteAlternateForCompoundWord(string word1, string word2)
		{
			if ((word1 == null) || (word2 == null))
			{
				throw new ArgumentNullException((word1 == null) ? "word1" : "word2");
			}

			if (!SupportsSpelling)
			{
				throw new InvalidOperationException();
			}

			if (word2.Length != 1)
			{
				if ((word1.Length > 1) && IsAllUpperCase(word1))
				{
					return null;
				}

				if ((word2.Length > 1) && IsAllUpperCase(word2))
				{
					return null;
				}

				string key = word1 + word2;
				if (discreteWordExceptions.ContainsKey(key))
				{
					return null;
				}

				if (ContainsNonLetter(key))
				{
					return null;
				}

				if (IsMixedCase(key))
				{
					key = key.ToLower(culture);
				}

				if (CheckSpelling(key) == WordSpelling.SpelledCorrectly)
				{
					string str2 = char.ToLower(word2[0], culture) + word2.Substring(1);
					return word1 + str2;
				}
			}

			return null;
		}

		/// <summary>
		/// The get preferred alternate for deprecated word.
		/// </summary>
		/// <param name="word">
		/// The word.
		/// </param>
		/// <returns>
		/// The System.String.
		/// </returns>
		public string GetPreferredAlternateForDeprecatedWord(string word)
		{
			if (word == null)
			{
				throw new ArgumentNullException("word");
			}

			return alternatesForDeprecatedWords[word];
		}

		/// <summary>
		/// The is casing exception.
		/// </summary>
		/// <param name="word">
		/// The word.
		/// </param>
		/// <returns>
		/// The System.Boolean.
		/// </returns>
		public bool IsCasingException(string word)
		{
			if (word == null)
			{
				throw new ArgumentNullException("word");
			}

			return casingExceptions.ContainsKey(word);
		}

		#endregion

		#region Methods

		internal static CultureInfo TryParseCulture(string cultureName)
		{
			try
			{
				return CultureInfo.GetCultureInfo(cultureName);
			}
			catch (ArgumentException)
			{
			}

			return null;
		}

		internal bool IsAlwaysMisspelledWord(string word)
		{
			return spellChecker != null && spellChecker.AlwaysMisspelledWords.Contains(word);
		}

		internal bool IsIgnoredWord(string word)
		{
			return spellChecker != null && spellChecker.IgnoredWords.Contains(word);
		}

		/// <summary>
		/// The dispose.
		/// </summary>
		/// <param name="disposing">
		/// The disposing.
		/// </param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				try
				{
					if (spellChecker != null)
					{
						spellChecker.Dispose();
					}
				}
				finally
				{
					spellChecker = null;
				}
			}
		}

		private static bool ContainsNonLetter(string word)
		{
			return word.Any(ch => !char.IsLetter(ch));
		}

		/// <summary>
		/// Called when the file changes.
		/// </summary>
		/// <param name="source">
		/// The source of the event.
		/// </param>
		/// <param name="e">
		/// The FileSystemEventArgs for the changing file.
		/// </param>
		private static void FileChanged(object source, FileSystemEventArgs e)
		{
			ClearCachedServices();
		}

		private static bool IsAllUpperCase(string word)
		{
			return word.All(ch => !char.IsLower(ch));
		}

		private static bool IsMixedCase(string word)
		{
			bool flag = false;
			bool flag2 = false;
			foreach (char ch in word)
			{
				if (char.IsUpper(ch))
				{
					flag = true;
				}
				else
				{
					flag2 = true;
				}

				if (flag && flag2)
				{
					return true;
				}
			}

			return false;
		}

		private static void LoadWordsFromXml(IDictionary<string, string> list, XmlDocument document, string xPathQuery, string attributeName)
		{
			XmlNodeList xmlNodeList = document.SelectNodes(xPathQuery);
			if (xmlNodeList == null)
			{
				return;
			}

			foreach (XmlNode node in xmlNodeList)
			{
				string str = string.Empty;
				if (attributeName != null)
				{
					if (node.Attributes != null)
					{
						XmlAttribute attribute = node.Attributes[attributeName];
						if (attribute != null)
						{
							str = attribute.Value;
						}
					}
				}

				list[node.InnerText] = str;
			}
		}

		/// <summary>
		/// Called when the file renames.
		/// </summary>
		/// <param name="source">
		/// The source of the event.
		/// </param>
		/// <param name="e">
		/// The RenamedEventArgs for the changing file.
		/// </param>
		private static void OnRenamed(object source, RenamedEventArgs e)
		{
			ClearCachedServices();
		}

		/// <summary>
		/// Creates a FileWatcher.
		/// </summary>
		/// <param name="path">
		/// The file to watch.
		/// </param>
		private void AddFileWatcher(string path)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return;
			}

			FileSystemWatcher watch = new FileSystemWatcher();
			string directoryName = Path.GetDirectoryName(path);
			watch.Path = directoryName;
			watch.Filter = Path.GetFileName(path);
			watch.Changed += FileChanged;
			watch.Created += FileChanged;
			watch.Deleted += FileChanged;
			watch.Renamed += OnRenamed;
			watch.EnableRaisingEvents = true;
		}

		private void AddWordsToCollection(ICollection<string> collection, IEnumerable<string> wordsToAdd)
		{
			foreach (string word in wordsToAdd)
			{
				if (!collection.Contains(word) && WordCollection.IsValidWordLength(word))
				{
					collection.Add(word);
					collection.Add(word.ToLower(culture));
					collection.Add(char.ToUpper(word.ToLower(culture)[0], culture) + word.ToLower(culture).Substring(1));
				}
			}
		}

		private void AddWordsToSpellChecker(IDictionary<string, string> ignoredWords, IDictionary<string, string> alwaysMisspelledWords)
		{
			if (spellChecker == null)
			{
				return;
			}

			if (ignoredWords != null)
			{
				AddWordsToCollection(spellChecker.IgnoredWords, ignoredWords.Keys);
			}

			if (alwaysMisspelledWords != null)
			{
				AddWordsToCollection(spellChecker.AlwaysMisspelledWords, alwaysMisspelledWords.Keys);
			}
		}

		private IDictionary<string, string> CreateCaseInsensitiveDictionary()
		{
			return new NullIfNotFoundDictionary<string, string>(StringComparer.Create(culture, true));
		}

		private IDictionary<string, string> CreateDictionary()
		{
			return new NullIfNotFoundDictionary<string, string>(StringComparer.Create(culture, false));
		}

		private void InitCustomDictionaries()
		{
			casingExceptions = CreateDictionary();
			alternatesForDeprecatedWords = CreateCaseInsensitiveDictionary();
			compoundAlternatesForDiscreteWords = CreateCaseInsensitiveDictionary();
			discreteWordExceptions = CreateCaseInsensitiveDictionary();
			dictionaryFolders = new Collection<string>();

			customDictionaryHashCode = 0;
			ScanAndLoadDictionaries(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
		}

		private void LoadCustomDic(string fileName)
		{
			IDictionary<string, string> ignoredWords = CreateCaseInsensitiveDictionary();
			using (StreamReader reader = new StreamReader(fileName))
			{
				string str;
				while ((str = reader.ReadLine()) != null)
				{
					ignoredWords[str] = string.Empty;
				}
			}

			AddFileWatcher(fileName);

			customDictionaryHashCode ^= File.GetLastWriteTime(fileName).GetHashCode();

			AddWordsToSpellChecker(ignoredWords, null);
		}

		private void LoadCustomDictionaryXml(string fileName)
		{
			XmlDocument document = new XmlDocument();
			try
			{
				document.Load(fileName);
			}
			catch (FileNotFoundException)
			{
			}
			catch (XmlException)
			{
			}

			AddFileWatcher(fileName);
			customDictionaryHashCode ^= File.GetLastWriteTime(fileName).GetHashCode();

			IDictionary<string, string> list = CreateCaseInsensitiveDictionary();
			IDictionary<string, string> dictionary2 = CreateCaseInsensitiveDictionary();
			LoadWordsFromXml(list, document, "/Dictionary/Words/Recognized/Word", null);
			LoadWordsFromXml(dictionary2, document, "/Dictionary/Words/Unrecognized/Word", null);
			LoadWordsFromXml(compoundAlternatesForDiscreteWords, document, "/Dictionary/Words/Compound/Term", "CompoundAlternate");
			LoadWordsFromXml(discreteWordExceptions, document, "/Dictionary/Words/Compound/Term", null);
			LoadWordsFromXml(discreteWordExceptions, document, "/Dictionary/Words/DiscreteExceptions/Term", null);
			LoadWordsFromXml(casingExceptions, document, "/Dictionary/Acronyms/CasingExceptions/Acronym", null);
			LoadWordsFromXml(alternatesForDeprecatedWords, document, "/Dictionary/Words/Deprecated/Term", "PreferredAlternate");
			AddWordsToSpellChecker(list, dictionary2);
		}

		private void ScanAndLoadDictionaries(string directory)
		{
			if (!string.IsNullOrEmpty(directory))
			{
				foreach (string str in Directory.GetFiles(directory, "CustomDictionary.xml", SearchOption.AllDirectories))
				{
					LoadCustomDictionaryXml(str);
				}

				foreach (string str in Directory.GetFiles(directory, string.Format("CustomDictionary.{0}.xml", culture.Name), SearchOption.AllDirectories))
				{
					LoadCustomDictionaryXml(str);
				}

				foreach (string str in Directory.GetFiles(directory, string.Format("CustomDictionary.{0}.xml", culture.Parent.Name), SearchOption.AllDirectories))
				{
					LoadCustomDictionaryXml(str);
				}

				foreach (string str2 in Directory.GetFiles(directory, "custom.dic", SearchOption.AllDirectories))
				{
					LoadCustomDic(str2);
				}

				foreach (string str2 in Directory.GetFiles(directory, string.Format("custom.{0}.dic", culture.Name), SearchOption.AllDirectories))
				{
					LoadCustomDic(str2);
				}

				foreach (string str2 in Directory.GetFiles(directory, string.Format("custom.{0}.dic", culture.Parent.Name), SearchOption.AllDirectories))
				{
					LoadCustomDic(str2);
				}
			}
		}

		#endregion
	}

	public class WordSpelling
	{
		public static WordSpelling SpelledCorrectly { get; set; }
	}

	internal class WordCollection
	{
		public static bool IsValidWordLength(string word)
		{
			throw new NotImplementedException();
		}
	}

	internal class NullIfNotFoundDictionary<T, T1> : IDictionary<string, string>
	{
		public NullIfNotFoundDictionary(StringComparer create)
		{
			throw new NotImplementedException();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		public void Add(KeyValuePair<string, string> item)
		{
			throw new NotImplementedException();
		}

		public void Clear()
		{
			throw new NotImplementedException();
		}

		public bool Contains(KeyValuePair<string, string> item)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public bool Remove(KeyValuePair<string, string> item)
		{
			throw new NotImplementedException();
		}

		public int Count { get; }
		public bool IsReadOnly { get; }
		public bool ContainsKey(string key)
		{
			throw new NotImplementedException();
		}

		public void Add(string key, string value)
		{
			throw new NotImplementedException();
		}

		public bool Remove(string key)
		{
			throw new NotImplementedException();
		}

		public bool TryGetValue(string key, out string value)
		{
			throw new NotImplementedException();
		}

		public string this[string key]
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}

		public ICollection<string> Keys { get; }
		public ICollection<string> Values { get; }
	}

	internal class StyleCopCore
	{
		public static PlatformID PlatformID;
	}

	internal sealed class SpellChecker : IDisposable
	{
		public ICollection<string> AlwaysMisspelledWords { get; set; }
		public ICollection<string> IgnoredWords { get; set; }

		public static SpellChecker FromCulture(CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		public WordSpelling Check(string word)
		{
			throw new NotImplementedException();
		}

		public int GetDependantFilesHashCode()
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
		}
	}
}
#endif