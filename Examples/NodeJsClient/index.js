var net = require('net');

var client = new net.Socket();

client.connect(11000, '127.0.0.1', function () {
    console.log('I am connected to the awsome server');
    client.write('Hello, server! Love, Client.<EOM>');
});

client.on('data', function (data) {
    console.log('The server says: ' + data);
    client.destroy(); // kill client after server's response
});

client.on('close', function () {
    console.log('Connection closed');
});
