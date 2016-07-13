var fs = require('fs');
var path = require('path');

var file = './src/NetMQ/project.json';
var version = process.env.APPVEYOR_REPO_TAG_NAME;

var jsonText = fs.readFileSync(path.join(__dirname, file), {
    encoding: 'utf8'
});

var project = JSON.parse(jsonText);
project.version = version;
jsonText = JSON.stringify(project);
fs.writeFileSync(file, jsonText, {encoding:'utf8'})