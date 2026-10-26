using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StudentRecordsDemo
{
    public partial class Form1 : Form
    {
        private const string SupabaseUrl =
            "https://hyaytewhbuosxnbhdugi.supabase.co";

        private const string PublishableKey =
            "sb_publishable_e5_oHGmKSIvKkjsPd-SU0g_U8Ehygkd";

        private static readonly HttpClient http =
            new HttpClient();

        private string accessToken = null;
        public Form1()
        {
            InitializeComponent();
            btnLogin.Click += btnLogin_Click;
            btnAddStudent.Click += btnAddStudent_Click;
            btnRefresh.Click += btnRefresh_Click;
            btnDelete.Click += btnDelete_Click;

            txtPassword.UseSystemPasswordChar = true;

            nudYearLevel.Minimum = 1;
            nudYearLevel.Maximum = 6;
            nudYearLevel.Value = 1;

            dgvStudents.ReadOnly = true;
            dgvStudents.AllowUserToAddRows = false;
            dgvStudents.SelectionMode =
                DataGridViewSelectionMode.FullRowSelect;
            dgvStudents.MultiSelect = false;
            dgvStudents.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.Fill;

            SetLoggedIn(false);
        }

        private void Form1_Load(object sender, EventArgs e)
        { }
            private void SetLoggedIn(bool loggedIn)
        {
            btnAddStudent.Enabled = loggedIn;
            btnRefresh.Enabled = loggedIn;
            btnDelete.Enabled = loggedIn;

            txtFullName.Enabled = loggedIn;
            txtCourse.Enabled = loggedIn;
            nudYearLevel.Enabled = loggedIn;
        }

        private HttpRequestMessage CreateRequest(
            HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(
                method, SupabaseUrl + path);

            request.Headers.Add("apikey", PublishableKey);

            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer", accessToken);
            }

            return request;
        }

        // 1. Log in using Supabase Auth.
        private async void btnLogin_Click(
            object sender, EventArgs e)
        {
            try
            {
                btnLogin.Enabled = false;
                lblStatus.Text = "Signing in...";

                var credentials = new
                {
                    email = txtEmail.Text.Trim(),
                    password = txtPassword.Text
                };

                using (var request = CreateRequest(
                    HttpMethod.Post,
                    "/auth/v1/token?grant_type=password"))
                {
                    request.Content = new StringContent(
                        JsonConvert.SerializeObject(credentials),
                        Encoding.UTF8,
                        "application/json");

                    using (var response =
                        await http.SendAsync(request))
                    {
                        string body =
                            await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new Exception(
                                "Login failed: " + body);
                        }

                        var result =
                            JsonConvert.DeserializeObject<LoginResult>(
                                body);

                        if (result == null ||
                            string.IsNullOrWhiteSpace(result.AccessToken))
                        {
                            throw new Exception(
                                "No access token was returned.");
                        }

                        accessToken = result.AccessToken;
                    }
                }

                SetLoggedIn(true);
                lblStatus.Text = "Connected to Supabase.";

                await LoadStudentsAsync();
            }
            catch (Exception ex)
            {
                accessToken = null;
                SetLoggedIn(false);
                lblStatus.Text = "Login failed.";

                MessageBox.Show(
                    ex.Message, "Login Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnLogin.Enabled = true;
            }
        }

        // 2. Retrieve student records.
        private async Task LoadStudentsAsync()
        {
            try
            {
                lblStatus.Text = "Loading students...";

                using (var request = CreateRequest(
                    HttpMethod.Get,
                    "/rest/v1/students?select=id,full_name,course,year_level,created_at&order=id.asc"))
                using (var response = await http.SendAsync(request))
                {
                    string body =
                        await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception(
                            "Could not load students: " + body);
                    }

                    var students =
                        JsonConvert.DeserializeObject<List<StudentRecord>>(
                            body);

                    dgvStudents.DataSource = students;

                    lblStatus.Text =
                        "Students loaded: " +
                        (students == null ? 0 : students.Count);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Could not load records.";

                MessageBox.Show(
                    ex.Message, "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // 3. Add a student.
        private async void btnAddStudent_Click(
            object sender, EventArgs e)
        {
            string name = txtFullName.Text.Trim();
            string course = txtCourse.Text.Trim();
            int year = (int)nudYearLevel.Value;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(course))
            {
                MessageBox.Show(
                    "Please enter the student's name and course.");
                return;
            }

            try
            {
                btnAddStudent.Enabled = false;
                lblStatus.Text = "Saving student...";

                var newStudent = new
                {
                    full_name = name,
                    course = course,
                    year_level = year
                };

                using (var request = CreateRequest(
                    HttpMethod.Post,
                    "/rest/v1/students"))
                {
                    request.Headers.TryAddWithoutValidation(
                        "Prefer", "return=minimal");

                    request.Content = new StringContent(
                        JsonConvert.SerializeObject(newStudent),
                        Encoding.UTF8,
                        "application/json");

                    using (var response =
                        await http.SendAsync(request))
                    {
                        string body =
                            await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new Exception(
                                "Could not save student: " + body);
                        }
                    }
                }

                txtFullName.Clear();
                txtCourse.Clear();
                nudYearLevel.Value = 1;

                MessageBox.Show("Student saved successfully!");

                await LoadStudentsAsync();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Save failed.";

                MessageBox.Show(
                    ex.Message, "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnAddStudent.Enabled =
                    !string.IsNullOrEmpty(accessToken);
            }
        }

        // 4. Refresh the records.
        private async void btnRefresh_Click(
            object sender, EventArgs e)
        {
            await LoadStudentsAsync();
        }

        // 5. Delete the selected student.
        private async void btnDelete_Click(
            object sender, EventArgs e)
        {
            if (dgvStudents.CurrentRow == null ||
                !(dgvStudents.CurrentRow.DataBoundItem
                    is StudentRecord))
            {
                MessageBox.Show(
                    "Please select a student to delete.");
                return;
            }

            var student =
                (StudentRecord)dgvStudents.CurrentRow.DataBoundItem;

            var answer = MessageBox.Show(
                "Delete " + student.FullName + "?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
                return;

            try
            {
                btnDelete.Enabled = false;
                lblStatus.Text = "Deleting student...";

                string path =
                    "/rest/v1/students?id=eq." + student.Id;

                using (var request = CreateRequest(
                    HttpMethod.Delete, path))
                using (var response = await http.SendAsync(request))
                {
                    string body =
                        await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception(
                            "Could not delete student: " + body);
                    }
                }

                await LoadStudentsAsync();
                lblStatus.Text = "Student deleted.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Delete failed.";

                MessageBox.Show(
                    ex.Message, "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnDelete.Enabled =
                    !string.IsNullOrEmpty(accessToken);
            }
        }
    }

    public class LoginResult
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
    }

    public class StudentRecord
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("course")]
        public string Course { get; set; }

        [JsonProperty("year_level")]
        public int YearLevel { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
        
