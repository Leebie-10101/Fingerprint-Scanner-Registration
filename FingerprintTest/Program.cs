using System;
using System.Drawing;
using System.IO;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using MySql.Data.MySqlClient;

namespace FingerprintEnrollment
{
    class Program : DPFP.Capture.EventHandler
    {
        private Capture Capturer;
        private Enrollment Enroller;
        private int UserIdInput;

        static void Main()
        {
            Program p = new Program();
            p.Init();

            Console.WriteLine("Enter User ID to enroll fingerprint:");
            p.UserIdInput = int.Parse(Console.ReadLine());

            p.Enroller = new Enrollment();

            Console.WriteLine("Place your finger on the scanner...");
            Console.WriteLine("Press ENTER to exit anytime.");
            Console.ReadLine(); // Keep program alive
        }

        // Initialize scanner
        public void Init()
        {
            try
            {
                Capturer = new Capture();
                if (Capturer != null)
                {
                    Capturer.EventHandler = this;
                    Capturer.StartCapture();
                    Console.WriteLine("Fingerprint scanner initialized.");
                }
                else
                {
                    Console.WriteLine("Cannot initialize fingerprint scanner.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error initializing scanner: " + ex.Message);
            }
        }

        // Event: Fingerprint captured
        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
        {
            Console.WriteLine("Fingerprint captured.");

            // Optional: save image
            var conv = new SampleConversion();
            Bitmap bitmap = null;
            conv.ConvertToPicture(Sample, ref bitmap);
            if (bitmap != null)
            {
                bitmap.Save("fingerprint.png");
                Console.WriteLine("Fingerprint image saved as fingerprint.png");
            }

            // Extract features for enrollment
            FeatureExtraction extractor = new FeatureExtraction();
            FeatureSet features = new FeatureSet();
            CaptureFeedback feedback = CaptureFeedback.None;

            extractor.CreateFeatureSet(Sample, DataPurpose.Enrollment, ref feedback, ref features);

            if (feedback != CaptureFeedback.Good)
            {
                Console.WriteLine("Poor quality fingerprint. Please try again.");
                return; // Wait for next scan
            }

            // Add features to enrollment
            Enroller.AddFeatures(features);

            switch (Enroller.TemplateStatus)
            {
                case Enrollment.Status.Ready:
                    // Enrollment complete
                    Template template = Enroller.Template;
                    byte[] templateBytes;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        template.Serialize(ms);
                        templateBytes = ms.ToArray();
                    }

                    SaveToDatabase(templateBytes, UserIdInput);
                    Console.WriteLine("Fingerprint enrollment complete for User ID: " + UserIdInput);
                    Capturer.StopCapture(); // Stop after success
                    break;

                case Enrollment.Status.Failed:
                    Console.WriteLine("Enrollment failed. Restarting...");
                    Enroller.Clear();
                    break;

                default:
                    // Show how many more scans are needed
                    Console.WriteLine($"Keep scanning... {Enroller.FeaturesNeeded} more sample(s) required.");
                    break;
            }
        }

        // Save fingerprint template to MySQL
        private void SaveToDatabase(byte[] templateBytes, int userId)
        {
            try
            {
                string connStr = "Server=localhost;Database=library_db;Uid=root;Pwd=;";
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "UPDATE users SET fingerprint_template=@fp WHERE id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@fp", templateBytes);
                        cmd.Parameters.AddWithValue("@id", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving to database: " + ex.Message);
            }
        }

        // Event Handlers
        public void OnFingerGone(object Capture, string ReaderSerialNumber)
            => Console.WriteLine("Finger removed.");

        public void OnFingerTouch(object Capture, string ReaderSerialNumber)
            => Console.WriteLine("Finger touched scanner.");

        public void OnReaderConnect(object Capture, string ReaderSerialNumber)
            => Console.WriteLine("Scanner connected.");

        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
            => Console.WriteLine("Scanner disconnected.");

        public void OnSampleQuality(object Capture, string ReaderSerialNumber, CaptureFeedback CaptureFeedback)
            => Console.WriteLine("Sample quality: " + CaptureFeedback);
    }
}
