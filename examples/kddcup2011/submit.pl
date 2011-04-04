#!/usr/bin/perl

# arguments: TRACK_NO FILE_NAME DESCRIPTION USER PASSWORD

use strict;
use warnings;

use WWW::Mechanize;
use Crypt::SSLeay;

die "arguments: TRACK_NO FILE_NAME DESCRIPTION USER PASSWORD\n" if @ARGV < 5;

my ($track_no, $filename, $description, $user, $password) = @ARGV;

die "description must not be longer than 50 characters: '$description'\n" if length $description > 50;

# TODO add some sanity checks: file size etc.

my $mech = WWW::Mechanize->new();

#$mech->credentials($user, $password);


# login
$mech->get('https://login.yahoo.com/');
my $response = $mech->submit_form(
        form_name => 'login_form',
        fields => {
                login  => $user,
                passwd => $password,
        }
);
die $response->status_line, "\n" unless $response->is_success;

$response = $mech->get('http://kddcup.yahoo.com/new_submission.php');
die $response->status_line, "\n" unless $response->is_success;

$response = $mech->submit_form(
        form_number => 1,
        fields => {
                track           => $track_no,
                description     => $description,
                validation_file => $filename,
        }
);
if ($response->is_success) {
        my $content = $response->decoded_content;
        
        my $current_score;
        my $best_score;
        
        if ($content =~ /The score for your last submission is <strong>(\d+\.\d+)<\/strong>/) {
                $current_score = $1;
        }
        if ($content =~ /Your best score is <strong>(\d+\.\d+)<\/strong>/) {
                $best_score = $1;
        }
        if (defined $current_score && defined $best_score) {
                print "current score:     $current_score\n";
                print "best score so far: $best_score\n";
                # TODO also print position of the submission
        }
        else {
                warn "$content\n\nCould not extract score from response.\n";
        }
}
else {
        print STDERR $response->status_line, "\n";
}