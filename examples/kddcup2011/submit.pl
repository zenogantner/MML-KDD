#!/usr/bin/perl

# arguments: TRACK_NO FILE_NAME DESCRIPTION USER PASSWORD

use strict;
use warnings;

use WWW::Mechanize;

die "arguments: TRACK_NO FILE_NAME DESCRIPTION USER PASSWORD\n" if @ARGV < 5;

my ($track_no, $filename, $description, $user, $password) = shift @ARGV;

my $mech = WWW::Mechanize->new();

$mech->credentials($user, $password);
$mech->get('http://kddcup.yahoo.com/new_submission.php');

$mech->submit_form(
        form_number => 1,
        fields => {
                track           => $track_no,
                description     => $description,
                validation_file => $filename,
        }
);
