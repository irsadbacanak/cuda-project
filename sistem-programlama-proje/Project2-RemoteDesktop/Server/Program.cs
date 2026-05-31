var server = new StreamServer(8888);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    server.Stop();
};

server.Start();
