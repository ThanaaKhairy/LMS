open System
open System.Windows.Forms
open Microsoft.Data.Sqlite

// Initialize SQLite database connection
let connectionString = "Data Source=Project_database.db"
use conn = new SqliteConnection(connectionString)
conn.Open()
printfn "Database connection successful."

// Ensure the database table exists
let ensureTableExists () =
    use cmd = new SqliteCommand(""" 
        CREATE TABLE IF NOT EXISTS Book (
            BookID INTEGER PRIMARY KEY,
            Title TEXT NOT NULL,
            Author_Name TEXT NOT NULL,
            Genre TEXT NOT NULL,
            IsBorrowed INTEGER DEFAULT 0,
            BorrowDate TEXT
        )
    """, conn)
    cmd.ExecuteNonQuery() |> ignore
ensureTableExists()

// Function to check if a book exists by BookId
let bookExists (conn: SqliteConnection) (bookId: int) =
    use cmd = new SqliteCommand("SELECT COUNT(*) FROM Book WHERE BookID = @bookId", conn)
    cmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
    let count = cmd.ExecuteScalar() :?> int64
    count > 0L

// Define the main form
let form = new Form(Text = "Library System", Width = 600, Height = 500)

// Create a DataGridView for displaying books
let dataGridView = new DataGridView(Dock = DockStyle.Bottom, Height = 250)
dataGridView.Columns.Add("BookID", "Book ID")
dataGridView.Columns.Add("Title", "Title")
dataGridView.Columns.Add("Author", "Author Name")
dataGridView.Columns.Add("Genre", "Genre")
dataGridView.Columns.Add("Status", "Status")

// Create the "Add" button
let addButton = new Button(Text = "Add", Left = 50, Top = 50, Width = 100)
addButton.Click.Add(fun _ -> 
    // Create a new form for adding books
    let addForm = new Form(Text = "Add New Book", Width = 400, Height = 300)

    // Create labels and textboxes
    let lblBookID = new Label(Text = "Book ID:", Left = 20, Top = 20)
    let txtBookID = new TextBox(Left = 150, Top = 20, Width = 200)

    let lblTitle = new Label(Text = "Title:", Left = 20, Top = 60)
    let txtTitle = new TextBox(Left = 150, Top = 60, Width = 200)

    let lblAuthor = new Label(Text = "Author Name:", Left = 20, Top = 100)
    let txtAuthor = new TextBox(Left = 150, Top = 100, Width = 200)

    let lblGenre = new Label(Text = "Genre:", Left = 20, Top = 140)
    let txtGenre = new TextBox(Left = 150, Top = 140, Width = 200)

    // Create the Submit button
    let btnSubmit = new Button(Text = "Submit", Left = 150, Top = 180, Width = 100)
    btnSubmit.Click.Add(fun _ -> 
        try
            let bookId = int txtBookID.Text
            let title = txtTitle.Text
            let author = txtAuthor.Text
            let genre = txtGenre.Text

            if String.IsNullOrWhiteSpace(title) || String.IsNullOrWhiteSpace(author) || String.IsNullOrWhiteSpace(genre) then
                MessageBox.Show("All fields must be filled.") |> ignore
            elif not (bookExists conn bookId) then
                use cmd = new SqliteCommand(""" 
                    INSERT INTO Book (BookID, Title, Author_Name, Genre, IsBorrowed, BorrowDate) 
                    VALUES (@bookId, @title, @auth, @genre, 0, NULL)
                """, conn)
                cmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
                cmd.Parameters.AddWithValue("@title", title) |> ignore
                cmd.Parameters.AddWithValue("@auth", author) |> ignore
                cmd.Parameters.AddWithValue("@genre", genre) |> ignore
                cmd.ExecuteNonQuery() |> ignore
                MessageBox.Show("Book added successfully!") |> ignore
                addForm.Close() // Close the add form after successful submission
            else
                MessageBox.Show("This BookID already exists in the database.") |> ignore
        with
        | :? FormatException -> 
            MessageBox.Show("Book ID must be a valid number.") |> ignore
    )

    // Add controls to the add form
    addForm.Controls.Add(lblBookID)
    addForm.Controls.Add(txtBookID)
    addForm.Controls.Add(lblTitle)
    addForm.Controls.Add(txtTitle)
    addForm.Controls.Add(lblAuthor)
    addForm.Controls.Add(txtAuthor)
    addForm.Controls.Add(lblGenre)
    addForm.Controls.Add(txtGenre)
    addForm.Controls.Add(btnSubmit)

    // Show the add form as a dialog
    addForm.ShowDialog() |> ignore
)
 // Create the "Borrow" button
let borrowButton = new Button(Text = "Borrow", Left = 50, Top = 120, Width = 100)
borrowButton.Click.Add(fun _ -> 
    let bookIdStr = Microsoft.VisualBasic.Interaction.InputBox("Enter Book ID to borrow:", "Borrow")
    if String.IsNullOrWhiteSpace(bookIdStr) then
        MessageBox.Show("Canceled successfully.") |> ignore
    else
        try
            let bookId = int bookIdStr
            if not (bookExists conn bookId) then
                MessageBox.Show("BookID does not exist.") |> ignore
            else
                use checkCmd = new SqliteCommand("SELECT IsBorrowed FROM Book WHERE BookID = @bookId", conn)
                checkCmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
                let isBorrowed = checkCmd.ExecuteScalar() :?> int64
                if isBorrowed = 1L then
                    MessageBox.Show("This book is already borrowed.") |> ignore
                else
                    let borrowDate = DateTime.Now.ToString("yyyy-MM-dd")
                    use updateCmd = new SqliteCommand("UPDATE Book SET IsBorrowed = 1, BorrowDate = @borrowDate WHERE BookID = @bookId", conn)
                    updateCmd.Parameters.AddWithValue("@borrowDate", borrowDate) |> ignore
                    updateCmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
                    updateCmd.ExecuteNonQuery() |> ignore
                    MessageBox.Show($"BookID {bookId} has been borrowed.") |> ignore
        with
        | :? FormatException ->
            MessageBox.Show("Please enter a valid number for Book ID.") |> ignore
)
