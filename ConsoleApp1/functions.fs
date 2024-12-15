open System
open System.Windows.Forms
open Microsoft.Data.Sqlite

let connectionString = "Data Source=Project_database.db"
use conn = new SqliteConnection(connectionString)
conn.Open()
printfn "Database connection successful."

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


let bookExists (conn: SqliteConnection) (bookId: int) =
    use cmd = new SqliteCommand("SELECT COUNT(*) FROM Book WHERE BookID = @bookId", conn)
    cmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
    let count = cmd.ExecuteScalar() :?> int64
    count > 0L

let form = new Form(Text = "Library System", Width = 660, Height = 500)

let dataGridView = new DataGridView(Dock = DockStyle.Bottom, Height = 250)
dataGridView.Columns.Add("BookID", "Book ID")
dataGridView.Columns.Add("Title", "Title")
dataGridView.Columns.Add("Author", "Author Name")
dataGridView.Columns.Add("Genre", "Genre")
dataGridView.Columns.Add("Status", "Status")
dataGridView.Columns.Add("BorrowDate", "Borrow Date")
// add Button
let addButton = new Button(Text = "Add", Left = 50, Top = 50, Width = 100)
addButton.Click.Add(fun _ ->
    let addForm = new Form(Text = "Add New Book", Width = 400, Height = 300)

    let lblBookID = new Label(Text = "Book ID:", Left = 20, Top = 20)
    let txtBookID = new TextBox(Left = 150, Top = 20, Width = 200)

    let lblTitle = new Label(Text = "Title:", Left = 20, Top = 60)
    let txtTitle = new TextBox(Left = 150, Top = 60, Width = 200)

    let lblAuthor = new Label(Text = "Author Name:", Left = 20, Top = 100)
    let txtAuthor = new TextBox(Left = 150, Top = 100, Width = 200)

    let lblGenre = new Label(Text = "Genre:", Left = 20, Top = 140)
    let txtGenre = new TextBox(Left = 150, Top = 140, Width = 200)

    let btnSubmit = new Button(Text = "Submit", Left = 150, Top = 180, Width = 100)
    btnSubmit.Click.Add(fun _ ->
        match txtBookID.Text, txtTitle.Text, txtAuthor.Text, txtGenre.Text with
        | bookIdStr, title, author, genre when String.IsNullOrWhiteSpace(bookIdStr)
                                                || String.IsNullOrWhiteSpace(title)
                                                || String.IsNullOrWhiteSpace(author)
                                                || String.IsNullOrWhiteSpace(genre) ->
            MessageBox.Show("All fields must be filled.") |> ignore
        | bookIdStr, title, author, genre ->
            try
                let bookId = int bookIdStr
                match bookExists conn bookId with
                | true -> MessageBox.Show("This BookID already exists in the database.") |> ignore
                | false ->
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
                    addForm.Close()
            with
            | :? FormatException -> MessageBox.Show("Book ID must be a valid number.") |> ignore
    )

    addForm.Controls.Add(lblBookID)
    addForm.Controls.Add(txtBookID)
    addForm.Controls.Add(lblTitle)
    addForm.Controls.Add(txtTitle)
    addForm.Controls.Add(lblAuthor)
    addForm.Controls.Add(txtAuthor)
    addForm.Controls.Add(lblGenre)
    addForm.Controls.Add(txtGenre)
    addForm.Controls.Add(btnSubmit)

    addForm.ShowDialog() |> ignore
)

// display Button
let displayButton = new Button(Text = "Display", Left = 200, Top = 50, Width = 100)
displayButton.Click.Add(fun _ ->
    dataGridView.Rows.Clear()

    use cmd = new SqliteCommand("SELECT BookID, Title, Author_Name, Genre, IsBorrowed, BorrowDate FROM Book", conn)
    use reader = cmd.ExecuteReader()

    let rec processRows () =
        match reader.Read() with
        | true ->
            let bookId = reader.GetInt32(0)
            let title = reader.GetString(1)
            let author = reader.GetString(2)
            let genre = reader.GetString(3)
            let status = match reader.GetInt64(4) with
                         | 1L -> "Borrowed"
                         | _ -> "Available"
            let borrowDate = if reader.IsDBNull(5) then "" else reader.GetString(5)
            dataGridView.Rows.Add(bookId, title, author, genre, status, borrowDate) |> ignore
            processRows ()
        | false -> ()

    if reader.HasRows then
        processRows ()
    else
        MessageBox.Show("No books found in the library.") |> ignore
)


// borrow Button
let borrowButton = new Button(Text = "Borrow", Left = 50, Top = 120, Width = 100)
borrowButton.Click.Add(fun _ ->
    let bookIdStr = Microsoft.VisualBasic.Interaction.InputBox("Enter Book ID to borrow:", "Borrow")
    match String.IsNullOrWhiteSpace(bookIdStr) with
    | true -> 
        MessageBox.Show("Canceled successfully.") |> ignore
    | false ->
        try
            let bookId = int bookIdStr
            match bookExists conn bookId with
            | false -> MessageBox.Show("BookID does not exist.") |> ignore
            | true ->
                use checkCmd = new SqliteCommand("SELECT IsBorrowed FROM Book WHERE BookID = @bookId", conn)
                checkCmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
                match checkCmd.ExecuteScalar() with
                | :? int64 as isBorrowed when isBorrowed = 1L ->
                    MessageBox.Show("This book is already borrowed.") |> ignore
                | _ ->
                    let borrowDate = DateTime.Now.ToString("yyyy-MM-dd")
                    use updateCmd = new SqliteCommand("UPDATE Book SET IsBorrowed = 1, BorrowDate = @borrowDate WHERE BookID = @bookId", conn)
                    updateCmd.Parameters.AddWithValue("@borrowDate", borrowDate) |> ignore
                    updateCmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
                    updateCmd.ExecuteNonQuery() |> ignore
                    MessageBox.Show($"BookID {bookId} has been borrowed.") |> ignore
        with
        | :? FormatException -> MessageBox.Show("Please enter a valid number for Book ID.") |> ignore
)

// return Button
let returnButton = new Button(Text = "Return", Left = 200, Top = 120, Width = 100)
returnButton.Click.Add(fun _ ->
    let bookIdStr = Microsoft.VisualBasic.Interaction.InputBox("Enter Book ID to return:", "Return")
    match String.IsNullOrWhiteSpace(bookIdStr) with
    | true -> 
        MessageBox.Show("Canceled successfully.") |> ignore
    | false ->
        try
            let bookId = int bookIdStr
            match bookExists conn bookId with
            | false -> MessageBox.Show("BookID does not exist.") |> ignore
            | true ->
                use checkCmd = new SqliteCommand("SELECT IsBorrowed FROM Book WHERE BookID = @bookId", conn)
                checkCmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
                match checkCmd.ExecuteScalar() with
                | :? int64 as isBorrowed when isBorrowed = 0L ->
                    MessageBox.Show("This book has not been borrowed.") |> ignore
                | _ ->
                    use updateCmd = new SqliteCommand("UPDATE Book SET IsBorrowed = 0, BorrowDate = NULL WHERE BookID = @bookId", conn)
                    updateCmd.Parameters.AddWithValue("@bookId", bookId) |> ignore
                    updateCmd.ExecuteNonQuery() |> ignore
                    MessageBox.Show($"BookID {bookId} has been returned.") |> ignore
        with
        | :? FormatException -> MessageBox.Show("Please enter a valid number for Book ID.") |> ignore
)



let searchButton = new Button(Text = "Search", Left = 350, Top = 50, Width = 100)
searchButton.Click.Add(fun _ -> 
    let title = Microsoft.VisualBasic.Interaction.InputBox("Enter the Title to search:", "Search by Title")

    match String.IsNullOrWhiteSpace(title) with
    | true -> 
        MessageBox.Show("Canceled successfully.") |> ignore
    | false -> 
        dataGridView.Rows.Clear()

        use cmd = new SqliteCommand("SELECT BookID, Title, Author_Name, Genre, IsBorrowed, BorrowDate FROM Book WHERE Title LIKE @title", conn)
        cmd.Parameters.AddWithValue("@title", "%" + title + "%") |> ignore
        use reader = cmd.ExecuteReader()

        let rec processRows () =
            match reader.Read() with
            | true -> 
                let bookId = reader.GetInt32(0)
                let bookTitle = reader.GetString(1)
                let author = reader.GetString(2)
                let genre = reader.GetString(3)
                let status = match reader.GetInt64(4) with
                             | 1L -> "Borrowed"
                             | _ -> "Available"
                let borrowDate = if reader.IsDBNull(5) then "" else reader.GetString(5)
                dataGridView.Rows.Add(bookId, bookTitle, author, genre, status, borrowDate) |> ignore
                processRows ()  
            | false -> ()  

        if reader.HasRows then
            processRows ()
        else
            MessageBox.Show("No books found with the given title") |> ignore
)


// add Button
form.Controls.Add(addButton)
form.Controls.Add(displayButton)
form.Controls.Add(borrowButton)
form.Controls.Add(searchButton)
form.Controls.Add(returnButton)
form.Controls.Add(dataGridView)

// run
Application.Run(form)
