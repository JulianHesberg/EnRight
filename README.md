# EnRight
A system that processes mail files, cleans them, indexes them, and allows users to search and download files.


To start the application use command:
- docker compose up --build (for the first time)
- docker compose up (to run a secound time after building is done)

All services are dockerised, u can access them on the following endpoints

**ZipKin:**
http://localhost:9411/

**Promethius:**
- http://localhost:9090/
- userName: admin
- pass: admin

**Grifana**
http://localhost:3030/

**RabbitMQ:**
- http://localhost:15672/
- userName: guest
- pass: guest

**MailCleaner:**
- In the backend/MailCleaner/maildir
- Add the folders containing the email files within this directory
- Make sure you do not add individual files, you must add the files with thier directory (The directory will be used for the FileName).
- If you add individual files the MailCleaner will not process them and move them into the Processed directory.

- Example:
-                    ./maildir/.allen_p/.sent_emails./{FileName}.
- You just need to make sure at least one subdirectoy is added inside maildir.


- Mailcleaner will go through each file, cleaning them and adding them to the cleaned_emails channel
- Mailcleaner will LOG success or failier and produce traces and matrics that can be seen in ZipKin and Promethius interfaces.
- RabbitMQ will add the files into the queue.

**Indexer:**
- Indexer should run manualy on it's own when it recives a message from RabbitMQ
- It is listning to the cleaned_emails channel, it will take files there and acknoaldge to rabbitMQ.
- Indexer will thes process the files adding them to the database.
- Indexer will LOG success or failier and produce traces and matrics that can be seen in ZipKin and Promethius interfaces
- Also the Controller will use Logs, Metrics and Traces.

**Frontend:**
- A basic interface was build to allow users to search for words.
- The front will send a request to the Indexer in which will query for results matching the user input.
- The indexer will return the files that matches the search result, sending them back to the front allowing the user to download them

- Indexer will return the top 20 files with the most Occurances for the Word the user is searching for
