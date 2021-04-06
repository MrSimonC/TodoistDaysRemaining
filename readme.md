# Todoist Days Remaining

Todoist comes with a free and Pro plan, however, either of which show the days remaining directly within a created task. On the mac desktop app, one can hover the mouse over a task to see days remaining, but nothing exists for mobile.

This project aims to set up an Azure Function, which authorises to the todoist API via API Key, reads tasks with their dates, then calculates the days remaining then appends this to each task, daily.