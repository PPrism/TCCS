using System.Diagnostics;
using System.IO.Compression;
using TCCS.XACTHandlers;

namespace TCCS
{
	class Program
    {
		private static readonly string[] CompressedTypes = new string[10]
        {
		    ".xpr",
		    ".txt",
		    ".str",
		    ".ps",
		    ".vs",
		    ".xma",
		    ".xsb",
		    ".xgs",
		    ".xwb",
		    ".fnt"
        };

		private static IEnumerable<(int index, T value)> Enumerate<T>(IEnumerable<T> Collection) => Collection.Select((Index, Value) => (Value, Index));

		private static void CopyDirectory(string SourcePath, string DestPath)
		{
			DirectoryInfo SourceInfo = new(SourcePath);
			Directory.CreateDirectory(DestPath);

			foreach (FileInfo TargetInfo in SourceInfo.GetFiles())
			{
				string TargetPath = Path.Combine(DestPath, TargetInfo.Name);
				TargetInfo.CopyTo(TargetPath);
			}

			DirectoryInfo[] Directories = SourceInfo.GetDirectories();
			foreach (DirectoryInfo Subs in Directories)
			{
				string NewDestPath = Path.Combine(DestPath, Subs.Name);
				CopyDirectory(Subs.FullName, NewDestPath);
			}
		}

		static void Main()
        {
			Console.WriteLine("Please ensure this tool is running in the same directory as Terraria's asset folder, which should be named 'Content', and the 'Prerequisites' folder.");
			Console.WriteLine("Press any key to continue:");
			Console.ReadKey();
			Console.Clear();
			Console.WriteLine("Please select what version this content folder is for:");
			Console.WriteLine("1) Initial Versions");
			Console.WriteLine("2) Version 1.01");
			Console.WriteLine("..or press any other key to exit.");
			Console.Write("\r\nType the number for which option you wish to select: ");

			byte VersionNum;
			switch (Console.ReadKey().Key)
			{
				case ConsoleKey.D1:
					VersionNum = 0;
					break;
				case ConsoleKey.D2:
					VersionNum = 1;
					break;
				default:
					return;
			}

			Console.Clear();

			for (int CheckIdx = 0; CheckIdx < 2; CheckIdx++)
			{
				string FolderName = (CheckIdx == 1) ? "Prerequisites" : "Content";
				Console.WriteLine("Now checking for the '" + FolderName + "' folder...");

				if (Directory.Exists(FolderName))
				{
					Console.WriteLine("Directory exists, proceeding...");
				}
				else
				{
					Console.WriteLine("Directory not found; Ensure this executable is placed in the same folder as the required folder.");
					return;
				}

				Thread.Sleep(1000);

				if (CheckIdx == 1)
				{
					Console.Clear();
				}
				else
				{
					Console.WriteLine();
				}
			}
			string ContentPath = Directory.GetCurrentDirectory() + @"\Content";
			string PrereqPath = Directory.GetCurrentDirectory() + @"\Prerequisites";
			string[] FileEntries = Directory.GetFiles(ContentPath, "*", SearchOption.AllDirectories);

			if (VersionNum == 0)
			{
				Console.WriteLine("For legal purposes, xDelta patches have been provided instead of source material. You will need xDelta v3.0.11 in the working directory of this suite.");
				Console.WriteLine("This is needed so that some original files can be transformed in to data that can be used with the decompilation.");
				Console.WriteLine("Type 'Y' once you have xDelta v3.0.11 in the same directory as this program and have named it 'xDelta3.exe':");
				ConsoleKeyInfo DeltaReady = Console.ReadKey();

				Console.WriteLine();

				if (DeltaReady.Key == ConsoleKey.Y)
				{
					if (File.Exists("xDelta3.exe"))
					{
						Console.WriteLine("Executable found; Proceeding...");
						Thread.Sleep(1000);
						Console.Clear();
					}
					else
					{
						Console.WriteLine("xDelta3 was not found in the current directory.");
						Console.WriteLine("You need xDelta3 in order to proceed.");
						return;
					}
				}
				else
				{
					Console.WriteLine("Input read failed, closing...");
					return;
				}

				if (!File.Exists(ContentPath + @"\SoundsOld.zip"))
				{
					ZipFile.CreateFromDirectory(ContentPath + @"\Sounds", ContentPath + @"\SoundsOld.zip");
				}
				Console.WriteLine("Patching...");
				Process Patcher = new();
				Patcher.StartInfo.FileName = "xDelta3.exe";
				Patcher.StartInfo.Arguments = string.Format("-d -s {0} {1} {2}", "\"" + ContentPath + @"\SoundsOld.zip" + "\"", "\"" + PrereqPath + @"\Patches\Sounds.pat" + "\"", "\"" + ContentPath + @"\SoundsNew.zip" + "\"");
				Patcher.StartInfo.UseShellExecute = false;
				Patcher.StartInfo.RedirectStandardOutput = true;
				Patcher.Start();
				Patcher.WaitForExit();

				Console.WriteLine("Extracting...");
				ZipFile.ExtractToDirectory(ContentPath + @"\SoundsNew.zip", ContentPath + @"\Sounds", true);
				Thread.Sleep(1000);
				File.Delete(ContentPath + @"\SoundsNew.zip");
				Console.WriteLine("Complete; proceeding...");
				Thread.Sleep(1000);
				Console.Clear();
			}

			if (VersionNum > 0)
			{
				Console.WriteLine("Content directories past the initial versions have compressed and encrypted data. This suite can handle them with the right tools.");
				Console.WriteLine("Is your content directory already decompressed and decrypted?");
				Console.WriteLine("Type 'Y' if no handling is needed, or type 'N' if you require the files to be worked on:");
				ConsoleKeyInfo DontTouch = Console.ReadKey();

				Console.WriteLine();

				if (DontTouch.Key == ConsoleKey.Y)
				{
					Console.WriteLine("Attempting to continue; Let's begin conversion...");
					Thread.Sleep(1000);
					Console.Clear();
				}
				else if (DontTouch.Key == ConsoleKey.N)
				{
					if (File.Exists("xbdecompress.exe") && File.Exists("unbundler.exe") && File.Exists("xma2encode.exe") && File.Exists("xwmaencode.exe"))
					{
						Console.WriteLine("Required executables found; Let's begin conversion...");
						Thread.Sleep(1000);
						Console.Clear();
					}
					else
					{
						Console.WriteLine("One of the following files is not found in the working directory:");
						Console.WriteLine("- xbdecompress.exe\n- unbundler.exe\n- xma2encode.exe\n- xwmaencode.exe");
						Console.WriteLine("Ensure these 4 files are placed in the same directory as this executable.");
						Console.WriteLine("You will also need the 3 decompressor dependencies: xbdm.dll, msvcp71.dll, and msvcr71.dll.");
						return;
					}
				}
				else
				{
					Console.WriteLine("Input read failed, closing...");
					return;
				}

				if (DontTouch.Key == ConsoleKey.N)
				{
					for (int FileIdx = 0; FileIdx < FileEntries.Length; FileIdx++)
					{
						FileInfo CurrentFile = new(FileEntries[FileIdx]);

						if (CompressedTypes.Contains(CurrentFile.Extension))
						{
							Process Decompresser = new();
							Decompresser.StartInfo.FileName = "xbdecompress.exe";
							Decompresser.StartInfo.Arguments = string.Format("/Y {0} {1}", "\"" + CurrentFile.FullName + "\"", "\"" + CurrentFile.Directory + "\""); // We need the escaped quotes to support paths with spaces in them.
							Decompresser.StartInfo.UseShellExecute = false;
							Decompresser.StartInfo.RedirectStandardOutput = true;
							Decompresser.Start();
							Decompresser.WaitForExit();

							if (CurrentFile.Extension == ".xpr")
							{
								try
								{
									Process Unbundler = new();
									Unbundler.StartInfo.FileName = "unbundler.exe";
									Unbundler.StartInfo.Arguments = "\"" + CurrentFile.FullName + "\"";
									Unbundler.StartInfo.UseShellExecute = false;
									Unbundler.StartInfo.RedirectStandardOutput = true;
									Unbundler.Start();
									Unbundler.WaitForExit();

									string BasicName = Path.GetFileNameWithoutExtension(CurrentFile.FullName);
									string Output = CurrentFile.FullName[..^3] + "tga";

									File.Move(BasicName + ".tga", Output); // Unbundler does not allow for the output file to be sent to the source directory, so we need to move the .tga files from the .xpr files we have.
									File.Delete(CurrentFile.FullName);
								}
								catch (FileNotFoundException) // There exist a couple of files that seem to be bundled incorrectly, so Unbundler cannot unpack a .tga file (or any file), leading to an exception; we need to continue regardless.
								{
									continue;
								}
							}

							if (CurrentFile.Extension == ".xma")
							{
								string Output = CurrentFile.FullName[..^3] + "wav";
								Process Converter = new();
								Converter.StartInfo.FileName = "xma2encode.exe";
								Converter.StartInfo.Arguments = string.Format("{0} /DecodeToPCM {1}", "\"" + CurrentFile.FullName + "\"", "\"" + Output + "\"");
								Converter.StartInfo.UseShellExecute = false;
								Converter.StartInfo.RedirectStandardOutput = true;
								Converter.Start();
								Converter.WaitForExit();
								File.Delete(CurrentFile.FullName); // For the purpose of efficiency, I'm gonna delete every source file that can produce another one (e.g. no .xma if we got a .wav)
							}
						}
					}
				}
			}

			string[] SongNames = Array.Empty<string>();
			DirectoryInfo RunDirectory = new(Environment.CurrentDirectory);
			string TotalDirectory = RunDirectory.FullName;
			for (int FileIdx = 0; FileIdx < FileEntries.Length; FileIdx++)
			{
				FileInfo CurrentFile = new(FileEntries[FileIdx]);
				string FullFileName = CurrentFile.FullName;
				string RelativePath = FullFileName[(TotalDirectory.Length + 1)..];

				if (CurrentFile.Extension == ".xgs")
				{
					_ = new XGSHandler(RelativePath, ContentPath + @"\Temp.xgs");
					File.Move(FullFileName, FullFileName + ".bak");
					File.Move(ContentPath + @"\Temp.xgs", FullFileName);
				}

				if (CurrentFile.Extension == ".xsb") // For Terraria, the Wave bank will be decompressed and loaded AFTER the sound bank has been decompressed...
				{
					XSBHandler SoundBank = new(RelativePath);
					SongNames = SoundBank.Songs.ToArray(); // The soundbanks contain the names of each song, so when you recreate the wavebank, you know what is what.
				}

				if (CurrentFile.Extension == ".xwb") // ..so no issues will be created doing it this way.
				{	
					XWBHandler WaveBank = new(RelativePath);
					Console.WriteLine("Writing music files...");

					foreach (var (Index, Entry) in Enumerate(WaveBank.Entries))
					{
						WaveWriter WaveWriter = new(Entry, EndianReader.Endianness.Little);
						if (SongNames.Length == 0)
						{
							WaveWriter.Write(Index, "Unknown", CurrentFile.DirectoryName); // If for some ungodly reason the soundbank does not have the names, you'll still have the placement index.
						}
						else
						{
							WaveWriter.Write(Index, SongNames[Index], CurrentFile.DirectoryName);
						}
					}
				}
			}

			Console.WriteLine("Wave bank successfully split; you will now need to convert these music files into an ADPCM XNA wave bank.");
			Console.WriteLine("For this, a tool like QuickWaveBank by Trigger is recommended. Make sure the files are inserted in order and the resulting wave bank is formatted as ADPCM.");
			Console.WriteLine("Press any key to continue:");
			Console.ReadKey();

			if (VersionNum == 2)
			{
				File.Move(ContentPath + @"\Music\Wave Bank.xwb", ContentPath + @"\Music\Wave Bank.xwb.bak");
			}
			else
			{
				File.Move(ContentPath + @"\Wave Bank.xwb", ContentPath + @"\Wave Bank.xwb.bak");
			}

			Console.Clear();

			Console.WriteLine("Moving stuff around...");
			Directory.Move(ContentPath + @"\Fonts", ContentPath + @"\FontsOld");
			CopyDirectory(PrereqPath + @"\Content\Fonts", ContentPath + @"\Fonts");
			CopyDirectory(PrereqPath + @"\Content\Achievements", ContentPath + @"\Achievements");
			CopyDirectory(PrereqPath + @"\Content\Images", ContentPath + @"\Images");
			CopyDirectory(PrereqPath + @"\Content\UI", ContentPath + @"\UI");
			File.Copy(PrereqPath + @"\Content\ACB.tga", ContentPath + @"\ACB.tga", true);
			File.Copy(PrereqPath + @"\Content\PEGI.xnb", ContentPath + @"\PEGI.xnb", true);
			File.Copy(PrereqPath + @"\Content\USK.xnb", ContentPath + @"\USK.xnb", true);
			Console.WriteLine();
			Console.WriteLine("Content folder setup completed successfully.");
			return;
		}
    }
}