# Changelog

## Implemented functionality
All the basic elements of the assignment are implemented. Sending and receiving normal user messages and processing client commands.   

### Program parameters 
`t` Transport protocol used for connection (tcp or udp)

`s` Server IP or hostname

`p` Server port (4567 default)

`d` UDP confirmation timeout (250ms default)

`r` Maximum number of UDP retransmissions (3 default)

### Implemented client input commands
`/auth {Username} {Secret} {DisplayName}` Sends AUTH message with the data provided from the command to the server (and correctly handles the Reply message), locally sets the DisplayName value (same as the /rename command).

`/join {ChannelID}` Sends JOIN message with channel name from the command to the server (and correctly handles the Reply message)

`/rename {DisplayName}` Locally changes the display name of the user to be sent with new messages/selected commands

`/help` Prints out supported local commands with their parameters and a description
   
## Known limitations 
There are no known application limitations