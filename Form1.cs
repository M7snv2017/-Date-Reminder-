using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Threading.Tasks;
using Telegram.Bot;
using System.Windows.Forms;
using System.Threading;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using dotenv.net;
using System.IO;

namespace PowerGym
{
    public partial class Form1 : Form
    {
        ReceiverOptions reOpt;
        Telegram.Bot.Types.Message m;
        public static TelegramBotClient master;
        bool[] state = { false, false, false };//year,month,day
        string[] res = { "", "", "" };
        string lastm = "";
        private System.Timers.Timer dailyTimer;
        //string filePath = @"ChatIds.txt";
        //string datePath = @"dates.txt";
        string filePath = @"C:\Users\M7sn9\OneDrive\Desktop\ChatIds.txt";
        string datePath = @"C:\Users\M7sn9\OneDrive\Desktop\dates.txt";
        public static TelegramBotClient botClient;
        UserManager userManager = new UserManager();
        private Dictionary<string, UserState> userStates = new Dictionary<string, UserState>();

        

        public struct UserData
        {
            public string id;
            public DateTime lastTargetDate;
        }

        public Form1()
        {
            InitializeComponent();
            

        }

        
        public async Task OnMessage1(ITelegramBotClient botClient, Update update, CancellationToken canceltoken)
        {
            if (update.Type != UpdateType.Message)
                return;

            var mes = update.Message;
            if (mes?.Text == null)
                return;

            if (update.Message is Telegram.Bot.Types.Message message)
            {
                try
                {
                    Console.WriteLine("Received message: " + message.Text);
                    m = message;
                    string userId = message.Chat.Id.ToString();

                    // Check if the user is in the userStates dictionary
                    if (!userStates.ContainsKey(userId))
                    {
                        userStates[userId] = new UserState();
                    }

                    var us = userStates[userId];

                    if (message.Text == "/start")
                    {
                        us.Reset();  // Reset state when the user starts over
                        lastm = "من فضلك أدخل السنة الميلادية (yyyy).";
                        await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                        us.IsExpectingYear = true; // Waiting for year

                        return;
                    }
                    else if (message.Text == "/save2remind") // Save to file
                    {
                        // No need to save again, the date has been saved already during the /start process
                        await botClient.SendTextMessageAsync(message.Chat.Id, "تم حفظ تاريخك بنجاح!");

                        // Save the current target date to the file
                        DateTime targetDate = new DateTime(int.Parse(us.Year), int.Parse(us.Month), int.Parse(us.Day));
                        WriteIdAndDateToFile(message.Chat.Id.ToString(), targetDate);

                        return;
                    }
                    else if (message.Text == "/showdate") // Show the saved date from file
                    {
                        // Show the saved target date for the user
                        
                        if (!IsIdNew(message.Chat.Id.ToString()))
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, $"تاريخك المحفوظ هو: {GetDateByIndex(GetIndexOfChatId(message.Chat.Id.ToString()))}.");
                            return;
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "لم تقم بحفظ تاريخ بعد.");
                            return;
                        }
                        
                    }
                    else if (message.Text == "/delete")
                    {
                        if (DeleteReminderDate(message.Chat.Id.ToString()))
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "حذفنا تاريخك المحفوظ.");
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, "لسبب ما لم يتم حذف تاريخك المحفوظ.");
                        }
                    }
                    else if (us.IsExpectingYear) // Expecting year
                    {
                        if (int.TryParse(message.Text, out int year) && year > 2024 && year < 9999)
                        {
                            us.Year = message.Text;
                            lastm = "من فضلك أدخل الشهر (mm).";
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                            us.IsExpectingYear = false;
                            us.IsExpectingMonth = true;
                        }
                        else
                        {
                            lastm = "السنة غير صحيحة.";
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                        }
                        return;
                    }
                    else if (us.IsExpectingMonth) // Expecting month
                    {
                        if (int.TryParse(message.Text, out int month) && month > 0 && month <= 12)
                        {
                            us.Month = message.Text;
                            lastm = "من فضلك أدخل اليوم (dd).";
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                            us.IsExpectingMonth = false;
                            us.IsExpectingDay = true;
                        }
                        else
                        {
                            lastm = "الشهر غير صحيح.";
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                        }
                        return;
                    }
                    else if (us.IsExpectingDay) // Expecting day
                    {
                        if (int.TryParse(message.Text, out int day) && day > 0 && day <= 31)
                        {
                            us.Day = message.Text;

                            // Construct the target date from the current session's input
                            DateTime targetDate = new DateTime(int.Parse(us.Year), int.Parse(us.Month), int.Parse(us.Day));
                            DateTime currentDate = DateTime.Now;

                            int daysLeft = (targetDate - currentDate).Days;

                            await botClient.SendTextMessageAsync(message.Chat.Id, $"تبقى {daysLeft} يوم حتى {targetDate.ToShortDateString()}.");
                            await botClient.SendTextMessageAsync(message.Chat.Id,  "\n اضغط على \n /save2remind \nاذا تريد ان تحفظ هذا التاريخ لنذكرك كل يوم كم تبقى على تاريخك");

                            // Save or update the user's target date
                            userManager.AddOrUpdateUser(userId, targetDate);
                            us.Reset();
                        }
                        else
                        {
                            lastm = "اليوم غير صحيح.";
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                        }
                        return;
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "😂😂");
                    }
                }
                catch (Exception e)
                {
                    // Handle invalid input
                    lastm = "الرجاء إدخال رقم إنجليزي.";
                    await botClient.SendTextMessageAsync(message.Chat.Id, lastm +"\n"+ e);
                    return;
                }
            }
        }


        private bool IsIdNew(string id)
        {
            if (System.IO.File.Exists(filePath))
            {
                var chatIds = System.IO.File.ReadAllLines(filePath);
                return !chatIds.Contains(id);
            }
            return true; // If file doesn't exist, treat as new ID
        }
        private void WriteIdAndDateToFile(string id, DateTime date)
        {
            if (IsIdNew(id))
            {
                System.IO.File.AppendAllText(filePath, id + Environment.NewLine); // Append ID to file
                System.IO.File.AppendAllText(datePath, date.ToString("yyyy-MM-dd") + Environment.NewLine); // Append date to file
            }
            else
            {
                int index = GetIndexOfChatId(id);

                var dates = System.IO.File.ReadAllLines(datePath).ToList();

                dates[index] = date.ToString("yyyy-MM-dd");

                System.IO.File.WriteAllLines(datePath, dates);
            }
        }
        private int GetIndexOfChatId(string id)
        {
            if (System.IO.File.Exists(filePath))
            {
                var chatIds = System.IO.File.ReadAllLines(filePath);
                return Array.IndexOf(chatIds, id); // Returns -1 if not found
            }
            return -1;
        }
        private string GetDateByIndex(int index)
        {
            if (System.IO.File.Exists(datePath))
            {
                var dates = System.IO.File.ReadAllLines(datePath);
                if (index >= 0 && index < dates.Length)
                {
                    if (DateTime.TryParse(dates[index], out DateTime date))
                    {
                        return date.ToString("M/d/yyyy"); // Format as "1/21/2026"
                    }
                }
            }
            return null;
        }

        private bool DeleteReminderDate(string id)
        {
            // Read all IDs and dates into lists
            var ids = System.IO.File.ReadAllLines(filePath).ToList();
            var dates = System.IO.File.ReadAllLines(datePath).ToList();

            // Find the index of the ID to delete
            int index = ids.IndexOf(id);

            // If the ID exists, remove it and its corresponding date
            if (index != -1)
            {
                ids.RemoveAt(index);   // Remove the ID
                dates.RemoveAt(index); // Remove the corresponding date

                // Write the updated lists back to their respective files
                System.IO.File.WriteAllLines(filePath, ids);
                System.IO.File.WriteAllLines(datePath, dates);
            }
            return IsIdNew(id);
        }

        private async void SendDailyMessage(object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.Now;

            if (now.Hour == 8 && now.Minute == 00)
            {
                if (System.IO.File.Exists(filePath) && System.IO.File.Exists(datePath))
                {
                    var chatIds = System.IO.File.ReadAllLines(filePath);
                    var targetDates = System.IO.File.ReadAllLines(datePath);

                    if (chatIds.Length != targetDates.Length)
                    {
                        Console.WriteLine($"Mismatch detected: {chatIds.Length} chat IDs and {targetDates.Length} target dates.");
                        return;
                    }

                    for (int i = 0; i < chatIds.Length; i++)
                    {
                        string chatIdStr = chatIds[i];
                        if (long.TryParse(chatIdStr, out long chatId))
                        {
                            if (DateTime.TryParse(targetDates[i], out DateTime targetDate))
                            {
                                int daysLeft = (targetDate - DateTime.Now).Days;

                                try
                                {
                                    await master.SendTextMessageAsync(chatId, $"⚠️ Days left until {targetDate.ToShortDateString()}: {daysLeft} days");

                                    if (daysLeft <= 0)
                                    {
                                        if (DeleteReminderDate(chatIdStr))
                                        {
                                            await master.SendTextMessageAsync(chatId, "حذفنا تاريخك المحفوظ.");
                                        }
                                        else
                                        {
                                            await master.SendTextMessageAsync(chatId, "لسبب ما لم يتم حذف تاريخك المحفوظ.");
                                        }
                                    }
                                }
                                catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                                {
                                    Console.WriteLine($"Error sending message to chat ID {chatId}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            DotNetEnv.Env.Load();
            var TELEGRAM_BOT_TOKEN = DotNetEnv.Env.GetString("BOT_TOKEN");
            // Initialize the TelegramBotClient with the token
            master = new TelegramBotClient(TELEGRAM_BOT_TOKEN.ToString());
            Console.WriteLine("Form1_Load is here");

            Task.Run(() => StratReciever());

            // Start daily message timer
            dailyTimer = new System.Timers.Timer(60000); // Check every minute
            dailyTimer.Elapsed += SendDailyMessage;
            dailyTimer.AutoReset = true;
            dailyTimer.Start();
        }
        public async Task HandleInvalidNumberInput(ITelegramBotClient botClient, Telegram.Bot.Types.Message message)
        {
            string errorMessage = "رقم انجليزي."; // Prompt user to enter a valid number
            await botClient.SendTextMessageAsync(message.Chat.Id, errorMessage);

            // Waiting for the user to enter a valid number can be handled here.
            // Depending on how you handle input continuation, you might loop back to OnMessage or directly retry from here.
        }
        public async Task ErrorMessage(ITelegramBotClient telegramBot, Exception e, CancellationToken cancellation)
        {
            if (e is ApiRequestException requestException)
            {
                Console.WriteLine($"Telegram API Error:\nError Code: [{requestException.ErrorCode}]\nMessage: {requestException.Message}");

                // Notify user about the error if possible
                if (m?.Chat != null)
                {
                    await telegramBot.SendTextMessageAsync(m.Chat.Id, "An error occurred. Please try again with valid input.");
                }
            }
            else
            {
                Console.WriteLine($"Unexpected Error: {e.Message}");
            }

            // Reset state to prevent bot from getting stuck
            Array.Clear(state, 0, state.Length);  // Resets the state array to all false
            lastm = "An error occurred. Please restart with /start.";
        }
        public async Task StratReciever()
        {
            var token = new CancellationTokenSource();
            var canceltoken = token.Token;
            reOpt = new ReceiverOptions { AllowedUpdates = { } };

            await master.ReceiveAsync(OnMessage1, ErrorMessage, reOpt, canceltoken);
        }
        public class UserManager
        {
            private UserData[] users = new UserData[15]; // Array to hold up to 15 users
            private int currentIndex = 0; // Track the current index for adding users

            // Method to add or update the user's target date
            public void AddOrUpdateUser(string userId, DateTime targetDate)
            {
                // Check if the user already exists in the array
                int index = Array.FindIndex(users, u => u.id == userId);

                if (index >= 0) // If user exists, update their date
                {
                    users[index].lastTargetDate = targetDate;
                }
                else
                {
                    // If the array is full, overwrite the first user
                    if (currentIndex >= users.Length)
                    {
                        currentIndex = 0; // Reset to the first position
                    }
                    // Add new user data
                    users[currentIndex] = new UserData { id = userId, lastTargetDate = targetDate };
                    currentIndex++; // Move to the next index
                }
            }

            // Method to get the saved date for a user
            public DateTime? GetSavedDate(string userId)
            {
                var user = Array.Find(users, u => u.id == userId);
                return user.id != null ? (DateTime?)user.lastTargetDate : null; // Return null if user not found
            }
        }
        public class UserState
        {
            public string Year { get; set; }
            public string Month { get; set; }
            public string Day { get; set; }
            public bool IsExpectingYear { get; set; }
            public bool IsExpectingMonth { get; set; }
            public bool IsExpectingDay { get; set; }

            // Reset the state for this user
            public void Reset()
            {
                IsExpectingYear = false;
                IsExpectingMonth = false;
                IsExpectingDay = false;
            }
        }

    }
}


