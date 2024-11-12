using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Timers;
using System.Threading.Tasks;
using Telegram.Bot;
using System.Windows.Forms;
using System.Threading;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

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
        string filePath = @"C:\Users\M7sn9\OneDrive\Desktop\ChatIds.txt";
        string datePath = @"C:\Users\M7sn9\OneDrive\Desktop\dates.txt";


        public Form1()
        {
            InitializeComponent();

        }
        public static TelegramBotClient botClient;
        private void Form1_Load(object sender, EventArgs e)
        {
            Console.WriteLine("Form1_Load is here");
            master = new TelegramBotClient("7317965896:AAHvyvbJc91j6gb3lyhaUIc1UQM5J4cuGUM");
            Task.Run(() => StratReciever());

            // Start daily message timer
            dailyTimer = new System.Timers.Timer(60000); // Check every minute
            dailyTimer.Elapsed += SendDailyMessage;
            dailyTimer.AutoReset = true;
            dailyTimer.Start();
        }


        public async Task StratReciever()
        {
            var token = new CancellationTokenSource();
            var canceltoken = token.Token;
            reOpt = new ReceiverOptions { AllowedUpdates = { } };

            await master.ReceiveAsync(OnMessage1, ErrorMessage, reOpt, canceltoken);
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
                    //if(message.Text.ToString()=="3")
                    //{
                    //    return;
                    //}
                    //Console.WriteLine("OnMessage1 is here");
                    Console.WriteLine("Received message: " + message.Text);
                    m = message;
                    if (message.Text == "/start")
                    {
                        state[1] = false; state[2] = false;
                        lastm = "ادخل السنه الميلادية ل انتهاء الاشتراك yyyy.";
                        await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                        state[0] = true;//waiting for year

                        return;
                    }
                    else if (state[0]) // Expecting year
                    {
                        if (int.TryParse(message.Text, out int year) && year > 2020 && year < 2040)
                        {
                            state[0] = false;
                            res[0] = message.Text;
                            lastm = "ادخل الشهر mm.";
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                            state[1] = true;
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                        }
                    }
                    else if (state[1]) // Expecting month
                    {
                        if (int.TryParse(message.Text, out int month) && month > 0 && month <= 12)
                        {
                            state[1] = false;
                            res[1] = message.Text;
                            lastm = "ادخل اليوم dd.";
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                            state[2] = true;
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                        }
                    }
                    else if (state[2]) // Expecting day
                    {
                        if (int.TryParse(message.Text, out int day) && day > 0 && day <= 31)
                        {
                            state[2] = false;
                            res[2] = message.Text;

                            DateTime targetDate = new DateTime(Convert.ToInt32(res[0]), Convert.ToInt32(res[1]), Convert.ToInt32(res[2])); 
                            DateTime currentDate = DateTime.Now;

                            int daysLeft = (targetDate - currentDate).Days;

                            await botClient.SendTextMessageAsync(message.Chat.Id, $"Days left until {targetDate.ToShortDateString()}: {daysLeft} days");
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat.Id, lastm);
                        }
                    }
                    else if(message.Text== "/reminder")
                    {
                        await Task.Run(()=>AddChatIdToFile(message.Chat.Id.ToString(),new DateTime(Convert.ToInt32(res[0]), Convert.ToInt32(res[1]), Convert.ToInt32(res[2]))));
                        await botClient.SendTextMessageAsync(message.Chat.Id, "we added you successfully!");
                        
                    }
                }
                catch (FormatException)
                {
                    //If the conversion fails, handle the error
                    await HandleInvalidNumberInput(botClient, message);
                    return;
                }
            }


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
        public async Task AddChatIdToFile(string chatId, DateTime targetDate)
        {
            // Check if the ID already exists
            if (!CheckIfIdExists(chatId))
            {
                // Add chat ID to the file
                using (StreamWriter writer = new StreamWriter(filePath, append: true))
                {
                    writer.WriteLine(chatId);
                }

                // Add target date to the date file
                using (StreamWriter writer = new StreamWriter(datePath, append: true))
                {
                    writer.WriteLine(targetDate.ToString("yyyy-MM-dd"));
                }

                Console.WriteLine($"ID: \"{chatId}\" and target date \"{targetDate.ToShortDateString()}\" have been added.");
            }
            else
            {
                Console.WriteLine($"ID: \"{chatId}\" already exists.");
            }
        }

        public bool CheckIfIdExists(string chatId)
        {
            // Read all IDs from the file and check if the chatId exists
            if (System.IO.File.Exists(filePath))
            {
                var chatIds = System.IO.File.ReadAllLines(filePath);
                return chatIds.Contains(chatId);
            }
            return false;
        }

        private async void SendDailyMessage(object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.Now;

            if (now.Hour == 6 && now.Minute == 00) // Check if it's exactly 5:00 AM
            {
                if (System.IO.File.Exists(filePath) && System.IO.File.Exists(datePath))
                {
                    var chatIds = System.IO.File.ReadAllLines(filePath);
                    var targetDates = System.IO.File.ReadAllLines(datePath);

                    for (int i = 0; i < chatIds.Length; i++)
                    {
                        string chatIdStr = chatIds[i];
                        if (long.TryParse(chatIdStr, out long chatId))
                        {
                            DateTime targetDate;
                            if (DateTime.TryParse(targetDates[i], out targetDate))
                            {
                                // Calculate days left
                                int daysLeft = (targetDate - DateTime.Now).Days;

                                // Send message to the user
                                await master.SendTextMessageAsync(chatId, $"Days left until {targetDate.ToShortDateString()}: {daysLeft} days");
                            }
                        }
                    }
                }
            }
        }
    }
}


