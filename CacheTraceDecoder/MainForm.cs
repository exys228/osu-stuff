using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using DictionaryProcessorLib;

namespace CacheTraceDecoder
{
	public partial class MainForm : Form
	{
		private static readonly Regex RegexObfuscated = new Regex("#=[a-zA-Z0-9_$]+={0,2}");
		private static Dictionary<string, string> _dictionary = new Dictionary<string, string>(); // obf => clean

		public MainForm()
		{
			AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
			{
				MessageBox.Show("An exception occurred! More details:\n" + eventArgs.ExceptionObject,
								"Unhandled exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
			};

			InitializeComponent();
		}

		private void browseButton_Click(object sender, EventArgs e)
		{
			if (cachePathDialog.ShowDialog() == DialogResult.OK)
				cachePathTextBox.Text = cachePathDialog.FileName;
		}

		private void cachePathTextBox_TextChanged(object sender, EventArgs e)
		{
			var path = cachePathTextBox.Text;

			if (File.Exists(path) && InitializeDictionary(path))
				cachePathTextBox.ForeColor = Color.Black;
			else
				cachePathTextBox.ForeColor = Color.Red;
		}

		private void inputTextBox_TextChanged(object sender, EventArgs e)
		{
			outputTextBox.Text = RegexObfuscated.Replace(inputTextBox.Text, match =>
			{
				if (_dictionary.TryGetValue(match.Value, out string val))
					return val;
				
				return match.Value;
			});
		}

		private bool InitializeDictionary(string path)
		{
			try
			{
				_dictionary = SwapKeysWithValues(DictionaryProcessor.Unpack(path));
				return true;
			}
			catch (DictionaryProcessorException) { return false; }
		}

		private static Dictionary<TValue, TKey> SwapKeysWithValues<TKey, TValue>(IDictionary<TKey, TValue> self)
		{
			var dict = new Dictionary<TValue, TKey>();

			foreach(var kvp in self)
				dict[kvp.Value] = kvp.Key;

			return dict;
		}
	}
}
