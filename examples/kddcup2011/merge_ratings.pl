#!/usr/bin/perl

use strict;
use warnings;

die "arguments: RATING_FILE_1 RATING_FILE_2\n" unless scalar @ARGV == 2;

my ($file1, $file2) = @ARGV;

open my $fh1, '<', $file1;
open my $fh2, '<', $file2;

while (<$fh1>) {
        my $line = $_; chomp $line;

        my @fields = split /\|/, $line;
        die "Cannot read line '$line'\n" unless @fields == 2;

        my ($user_id, $num_ratings) = @fields;
        my $user_ratings_ref = read_user_ratings($num_ratings, $fh1);

        $line = <$fh2>; chomp $line;

        @fields = split /\|/, $line;
        die "Cannot read line '$line'\n" unless @fields == 2;

        my ($user_id2, $num_ratings2) = @fields;
        my $user_ratings2_ref = read_user_ratings($num_ratings2, $fh2);        
        
        die "different users: $user_id, $user_id2\n" if $user_id != $user_id2;
        
        foreach my $key (keys %$user_ratings2_ref) {
                warn "Overlap: user $user_id, item $key\n" if exists $user_ratings_ref->{$key};
                
                $user_ratings_ref->{$key} = $user_ratings2_ref->{$key};
        }

        $num_ratings = scalar keys %$user_ratings_ref;
        print "$user_id|$num_ratings\n";
        foreach my $key (sort keys %$user_ratings_ref) {
                print "$key\t$user_ratings_ref->{$key}\n";
        }
}

sub read_user_ratings {
        my ($num_ratings, $fh) = @_;
        
        my %user_ratings = ();
        
        for (my $i = 0; $i < $num_ratings; $i++) {
                my $line = <$fh>;
                chomp $line;
                
                my ($item_id, $rating) = split /\t/, $line;
                
                $user_ratings{$item_id} = $rating;
        }

        return \%user_ratings;
}