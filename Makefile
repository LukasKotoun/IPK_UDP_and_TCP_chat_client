All:
	dotnet publish -c Release -p:PublishSingleFile=true --no-self-contained -p:DebugType=None -o .
clean:
	rm ./ipk24chat-client
	rm ./ipk24chat-client.xml