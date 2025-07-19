using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MusicPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        enum PlaybackState
        {
            Playing,
            Paused,
            Stopped
        }

        private PlaybackState _currentState = PlaybackState.Paused;

        private Random random;
        private MediaPlayer Player;
        private List<string> songs = new();
        private List<string> ignoreList = new();
        private static readonly BitmapImage placeholderImage = new(new Uri("pack://application:,,,/200.png"));

        private int currentSongIndex;
        private DispatcherTimer timer;

        private string[] extensionsToInclude = [".mp3", ".flac"];

        public class ListBoxItemData
        {
            public string Text { get; set; }
            public string Text2 { get; set; }
            public ImageSource Image { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            Player = new MediaPlayer();
            Player.MediaEnded += Player_MediaEnded;
            Player.MediaFailed += Player_MediaFailed;
            random = new Random((int)DateTime.Now.Ticks);

            if (File.Exists("ignorelist.txt"))
            {
                ignoreList = new List<string>(File.ReadAllLines("ignorelist.txt"));
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
        }

        private void Player_MediaFailed(object? sender, ExceptionEventArgs e)
        {
            ChangeState(PlaybackState.Stopped);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (Player.NaturalDuration.HasTimeSpan)
            {
                TimeSpan currentTime = Player.Position;
                TimeSpan totalTime = Player.NaturalDuration.TimeSpan;

                TimerText.Text = $"{currentTime:mm\\:ss} / {totalTime:mm\\:ss}";
                SongSeekBar.Value = (currentTime.TotalMilliseconds / totalTime.TotalMilliseconds);
            }
        }

        private void Player_MediaEnded(object? sender, EventArgs e)
        {
            PlayNextSong();
        }
        private void PlayNextSong()
        {
            currentSongIndex = (currentSongIndex + 1) % songs.Count;
            PlaySong(currentSongIndex);
        }

        private void PlaySong(int songIndex)
        {
            if (songs.Count <= 0)
                return;

            timer.Start();
            Player.Open(new Uri(songs[songIndex]));

            //var file = TagLib.File.Create(songs[songIndex]);

            using (var file = TagLib.File.Create(songs[songIndex])) { 
                var formattedAlbumString = file.Tag.Album?.Replace("Original Sound Track", "");

                NowPlayingText.Text = $"{formattedAlbumString} - {file.Tag.Title}";
                CurrentSongAlbumArtImage.Source = GetBitmapImageFromFilePath(songs[songIndex]);
                FilesListBox.SelectedItem = FilesListBox.Items[currentSongIndex];
                ChangeState(PlaybackState.Playing);
                FileNameText.Text = Path.GetFileName(songs[songIndex]);
                FilesListBox.ScrollIntoView(FilesListBox.SelectedItem);
            }
        }

        // Function to shuffle the list
        private static void ShuffleList<T>(List<T> list)
        {
            Random rand = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rand.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private void FilesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            currentSongIndex = FilesListBox.SelectedIndex;
            PlaySong(currentSongIndex);
        }

        private void AddSongsToFilesListBox(List<string> newSongs)
        {
            var index = 0;
            FilesListBox.Items.Clear();

            foreach (var file in newSongs)
            {
                //var taglibFile = TagLib.File.Create(file);

                using (var taglibFile = TagLib.File.Create(file))
                {

                    var formattedAlbumString = taglibFile.Tag.Album?.Replace("Original Sound Track", "");

                    var item = new ListBoxItemData
                    {
                        Text = $"{(index + 1).ToString("D2")}. {taglibFile.Tag.Title}",
                        Text2 = $"{formattedAlbumString} | {taglibFile.Properties.Duration.ToString(@"mm\:ss")}",
                        Image = GetBitmapImageFromFilePath(file) ?? placeholderImage
                    };
                    
                    FilesListBox.Items.Add(item);
                    index++;
                 }
            }
        }

        private BitmapImage? GetBitmapImageFromFilePath(string filePath)
        {
            using (var file = TagLib.File.Create(filePath))
            {

                BitmapImage bitmap = new BitmapImage();

                if (file.Tag.Pictures.Length > 0)
                {
                    var pic = file.Tag.Pictures[0];
                    byte[] imageData = pic.Data.Data;

                    using (var ms = new MemoryStream(imageData))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad; // Ensures stream can close
                        bitmap.StreamSource = ms;
                        bitmap.DecodePixelWidth = 100;

                        bitmap.EndInit();
                    }

                    return bitmap;
                }

                string directory = Path.GetDirectoryName(filePath);
                string path = Path.Combine(directory, "cover.png");
                if (!File.Exists(path))
                {
                    path = Path.Combine(directory, "cover.jpg");
                    if (!File.Exists(path))
                        return null;
                }

                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = 100;

                bitmap.EndInit();

                return bitmap;
            }
        }

        private void ChangePlayPauseButton()
        {
            if (_currentState == PlaybackState.Stopped) return;

            PlayPauseButton.Text = _currentState == PlaybackState.Paused ? "\uE768" : "\uE769";
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (songs.Count <= 0) return;

            AddToIgnoreList(songs[currentSongIndex]);
            PlayNextSong();
        }

        public void AddToIgnoreList(string songPath)
        {
            string ignoreListPath = "ignorelist.txt";

            // Check if the file exists
            if (!File.Exists(ignoreListPath))
            {
                // If the file does not exist, create it
                File.Create(ignoreListPath).Dispose();
            }

            // Add the song path to the ignore list
            File.AppendAllText(ignoreListPath, songPath + Environment.NewLine);
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (songs.Count == 0) return;


            switch (_currentState)
            {
                case PlaybackState.Playing:
                    ChangeState(PlaybackState.Paused);
                    break;
                case PlaybackState.Paused:
                    ChangeState(PlaybackState.Playing);
                    break;
                default:
                    break;
            }
        }

        private void ChangeState(PlaybackState newState)
        {
            _currentState = newState;
            switch (_currentState)
            {
                case PlaybackState.Playing:
                    if (Player.CanPause)
                        Player.Play();
                    else
                        PlaySong(currentSongIndex);
                    break;
                case PlaybackState.Paused:
                    Player.Pause();
                    break;
                case PlaybackState.Stopped:
                    break;
                default:
                    break;
            }
            ChangePlayPauseButton();
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Player == null) return;

            Player.Volume = e.NewValue / 100;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }

        private void PlayRandomSong_Click(object sender, RoutedEventArgs e)
        {
            currentSongIndex = random.Next(0, songs.Count);
            PlaySong(currentSongIndex);
        }

        private async Task ProcessSongAsync(string song, int minDurationInSeconds, List<string> ignoreList, List<string> validSongs)
        {
            try
            {
                var file = await Task.Run(() => TagLib.File.Create(song)); // Offload the blocking operation to a background thread

                var duration = file.Properties.Duration.TotalSeconds;

                if (duration >= minDurationInSeconds && !ignoreList.Contains(Path.GetFileName(song)))
                {
                    lock (validSongs) // Use lock to avoid concurrency issues with the list
                    {
                        validSongs.Add(song);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {song}: {ex.Message}");
            }
        }

        async Task<bool> AddSongsFromFolder(bool ShouldAppend = false)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new();

            dialog.Multiselect = false;
            dialog.Title = "Select a folder";

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                string fullPathToFolder = dialog.FolderName;
                string folderNameOnly = dialog.SafeFolderName;

                FolderNameText.Text = Path.GetFileName(fullPathToFolder);

                List<string> newsongs = Directory.GetFiles(fullPathToFolder, "*.*", SearchOption.AllDirectories)
                    .Where(file => extensionsToInclude
                    .Contains(Path.GetExtension(file)
                    .ToLower()))
                    .ToList();

                var validSongs = new List<string>();
                var minDurationInSeconds = 20;

                var tasks = new List<Task>(); // List to hold the tasks for concurrent processing

                // Process each song asynchronously
                foreach (var song in newsongs)
                {
                    tasks.Add(ProcessSongAsync(song, minDurationInSeconds, ignoreList, validSongs));
                }

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);

                if (ShouldAppend)
                    songs.AddRange(validSongs);
                else
                    songs = validSongs;

                ShuffleList(songs);
                AddSongsToFilesListBox(songs);
            }
            return true;
        }
        private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {            
            await AddSongsFromFolder(false);           
        }

        private async void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            await AddSongsFromFolder(true);
        }

        private void SongSeekBar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            long ticks = Player.NaturalDuration.TimeSpan.Ticks;
            ticks = (long)(ticks * SongSeekBar.Value);
            Player.Position = new TimeSpan(ticks);
        }

        private void ExitApplication_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}