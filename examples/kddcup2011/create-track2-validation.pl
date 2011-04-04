#!/usr/bin/perl

use strict;
use warnings;

my %ratings_by_user = ();

while (<>) {
        my $line = $_;
        chomp $line;

        my @fields = split /\|/, $line;
        die "Cannot read line '$line'\n" unless @fields == 2;

        my ($user_id, $num_ratings) = @fields;
        
        my $user_ratings = {};
        
        for (my $i = 0; $i < $num_ratings; $i++) {
                $line = <>;
                chomp $line;
                
                my ($item_id, $rating) = split /\t/, $line;
                $user_ratings->{$item_id} = $rating;
        }
        $ratings_by_user{$user_id} = $user_ratings;
}
