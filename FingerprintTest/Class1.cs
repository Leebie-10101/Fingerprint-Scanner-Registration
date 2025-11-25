using System;
using System.IO;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using DPFP.Verification;
using MySql.Data.MySqlClient;

namespace FingerprintVerification
{
    class Program : DPFP.Capture.EventHandler
    {
        private Capture Capturer;
        private Template StoredTemplate;
        private int UserIdToVerify;

        static void Main_2()
        {
            Program p = new Program();
            p.Init();

            Console.WriteLine("Enter User ID to verify fingerprint:");
            p.UserIdToVerify = int.Parse(Console.ReadLine());

            // Retrieve the fingerprint template from DB
            p.StoredTemplate = p.GetTemplateFromDatabase(p.UserIdToVerify);

            if (p.StoredTemplate == null)
            {
                Console.WriteLine("No fingerprint template found for that user.");
                return;
            }

            Console.WriteLine("Place your finger on the scanner to verify...");
            Console.WriteLine("Press ENTER to exit anytime.");
            Console.ReadLine(); // keep program alive
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

        // Retrieve fingerprint template from DB and convert to DPFP Template
        public Template GetTemplateFromDatabase(int userId)
        {
            try
            {
                string connStr = "Server=localhost;Database=library_db;Uid=root;Pwd=;";
                using (var conn = new MySqlConnection(connStr))
                {
                    conn.Open();
                    string sql = "SELECT fingerprint_template FROM users WHERE id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", userId);
                        byte[] templateBytes = cmd.ExecuteScalar() as byte[];

                        if (templateBytes != null)
                        {
                            using (MemoryStream ms = new MemoryStream(templateBytes))
                            {
                                return new Template(ms); // ✅ Correct usage
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving template: " + ex.Message);
                return null;
            }
        }

        // Event: Fingerprint captured
        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
        {
            Console.WriteLine("Fingerprint captured.");

            // Extract features from live sample for verification
            FeatureExtraction extractor = new FeatureExtraction();
            FeatureSet liveFeatures = new FeatureSet();
            CaptureFeedback feedback = CaptureFeedback.None;

            extractor.CreateFeatureSet(Sample, DataPurpose.Verification, ref feedback, ref liveFeatures);

            if (feedback != CaptureFeedback.Good)
            {
                Console.WriteLine("Poor quality fingerprint. Try again.");
                return;
            }

            // Verify against the stored template
            Verification verif = new Verification();
            Verification.Result result = new Verification.Result();

            verif.Verify(liveFeatures, StoredTemplate, ref result);

            if (result.Verified)
                Console.WriteLine($"Fingerprint matches user ID {UserIdToVerify}!");
            else
                Console.WriteLine("Fingerprint does NOT match.");
        }

        // Event handlers for scanner
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
